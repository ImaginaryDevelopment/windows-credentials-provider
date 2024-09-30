module CredentialHelper.CompositionRoot

open System.Threading
open System.Threading.Tasks

open BReusable

type ErrorType = string

open QRCode

type ImageDelegateType = CancellationToken -> DisposalTracker<System.Drawing.Bitmap> option
type QrDelegateType = DisposalTracker<System.Drawing.Bitmap> * CancellationToken -> Task<QrResult>
type CredentialDelegateType = ApiClient.AuthPost * CancellationToken -> Task<Result<ApiClient.VerificationResult,CameraControl.ApiResult>>

let getImage (qrControl:CameraControl.CameraControl) : ImageDelegateType =
    fun ct ->
        if ct.IsCancellationRequested then
            None
        else
            match qrControl.TakeSnap() with
            | Ok bm ->
                printfn $"GotImage:{bm.GetText()}-IsDisposed?{bm.IsDisposed}"
                Some bm
            | Error e ->
                eprintfn "Failed to read image: %s" e
                None

let tryGetQrCode (qrManager: QrManager): QrDelegateType =
    fun (dispBm,ct) ->
        task {
            if ct.IsCancellationRequested then
                return QrResult.QrNotFound
            else
                if dispBm.IsDisposed then
                    failwith $"Fetched image is already disposed"
                match dispBm.TryGet("tryGetQrCode") with
                | None -> return QrResult.QrNotFound
                | Some bm ->
                    return qrManager.TryDecode (bm,ct)
        }

let verifyQrCode config : CredentialDelegateType =
    fun (ap,ct) ->
        task {
            printfn "Verifying code: %A" ap.Code
            let value = CameraControl.UI.verifyQrCode config (ap.Code,ct)
            printfn "QrCode Verified? %A" value
            return  value
        }

let createVerifyQrCodeDelegate: _ -> CredentialDelegateType =
    fun config ->
        verifyQrCode config

let tryApiCall (appConfig,devApiUrl) qrCodeOpt =
    CredentialHelper.ApiClient.BaseUrl.TryCreate devApiUrl
    |> Result.bind(fun baseUrl ->
        let qrCodeValue = qrCodeOpt |> Option.defaultValue ""
        let ap : ApiClient.AuthPost = {Code=qrCodeValue}
        let r = CredentialHelper.ApiClient.tryValidate appConfig baseUrl (ap, CancellationToken.None) |> Async.AwaitTask |> Async.RunSynchronously
        r |> Result.mapError ApiClient.ApiResultError.ToErrorMessage
    )
    |> function
        | Ok v -> 
            printfn "%A" v
        | Error e ->
            eprintfn "%A" e

let outputDiagnostics dllComGuid =
    // registry
    CredentialHelper.Reusable.RegistryAdapters.Diag.outputDiagnostics dllComGuid
    // certificate
    CredentialHelper.Reusable.CertAdapters.outputDiagnostics()
    // dsregcmd
    CredentialHelper.Reusable.ProcessAdapters.DsRegCmd.getStatus()
    |> Result.map CredentialHelper.Reusable.ProcessAdapters.DsRegCmd.getWorkplaces
    |> printfn "%A"


type WorkerState =
    | NotStarted
    | Started
    | Paused
    | Stopped
type SystemState =
    {
        HasHandle:bool
        CameraRunning:bool
    }

// checkHandle checks for a handle, and if the camera is actually running
type Worker(checkHandle:System.Func<SystemState>, getImage: ImageDelegateType, tryGetQr: QrDelegateType, tryGetCredentials: CredentialDelegateType, onFound: ApiClient.VerificationResult -> unit, cts: System.Threading.CancellationTokenSource) =

    let l = obj()
    let checkCanRun () = checkHandle.Invoke()
    let hasHandle () = match checkCanRun() with | {HasHandle=true} -> true | _ -> false

    let mutable state = NotStarted
    let mutable result = None

    let trySet value =
        if not cts.IsCancellationRequested then
            lock l (fun () ->
                match result with
                // don't overwrite an ok with anything
                | Some (Ok _ ) -> ()
                | None
                | Some (Error _ ) ->
                    // lock could have taken time, check again
                    if not cts.IsCancellationRequested then
                        result <- Some value
            )

    // consider a disposable tracker here?
    let task = // tasks are hot
        task{
            let mutable systemState = checkCanRun()
            while not cts.IsCancellationRequested && (match systemState with | {HasHandle=false} | {CameraRunning=false} -> true | _ -> false) do
                do! Task.Delay(150, cts.Token)
                systemState <- checkCanRun()
                printfn "Looping: %A-%A" systemState state
            if not cts.IsCancellationRequested then
                state <- Started

            printfn "Started!"
            while not cts.IsCancellationRequested && hasHandle() do
                match state with
                | Started ->
                    match getImage cts.Token with
                    | None -> ()
                    | Some image ->
                        printfn "Found image?"
                        match! tryGetQr (image,cts.Token) with
                        | QrNotFound -> trySet (Error (Choice1Of2 "Qr not found"))
                        | QrCodeFound qrCode ->
                            printfn "Found QrCode"
                            if not cts.IsCancellationRequested then
                                match! tryGetCredentials ({Code=qrCode}, cts.Token) with
                                | Error e -> trySet (Error (Choice2Of2 e))
                                | Ok creds ->
                                    printfn "Found Qr Code: %A" creds.Username
                                    trySet (Ok creds)
                                    cts.Cancel()
                                    onFound creds
                | _ -> ()
                let delay =
                    match state with
                    | Paused -> 1000
                    | _ -> 150
                do! Task.Delay(delay, cts.Token)
                //printfn "Looping: %A" state
            state <- Stopped
        }

    new(checkHandle, qrControl, qrManager, config, onFound:System.Action<_>, cts) =
        new Worker(checkHandle, getImage qrControl,tryGetQrCode qrManager ,verifyQrCode config, onFound.Invoke, cts)

    member _.Start() =
        if not cts.IsCancellationRequested && state = NotStarted then
            state <- Started
        else
            invalidOp "Task already cancelled"

    member _.IsRunning =
        match state with
        | Started -> true
        | _ -> false

    member _.Pause
        with get() =
            match state with
            | Paused -> true
            | _ -> false
        and set pause =
            match state, pause with
            | Started,true ->
                state <- Paused
            | Paused, false ->
                state <- Started
            | _ -> ()

    member _.Stop() =
        if not cts.IsCancellationRequested then
            cts.Cancel()
            // do we need this? the task should set it
            state <- Stopped

    member _.TryGetResult() =
        match result with
        | None
        | Some (Error _) -> None
        | Some (Ok value) -> Some value

    member _.TryGetError() =
        match result with
        | None
        | Some(Ok _) -> None
        | Some(Error e) -> Some e
