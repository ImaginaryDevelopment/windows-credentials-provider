module CredentialHelper.CompositionRoot

open System.Threading
open System.Threading.Tasks

open Reusable

type ErrorType = string

open QRCode

type ImageDelegateType = CancellationToken -> DisposalTracker<System.Drawing.Bitmap> option
type QrDelegateType = DisposalTracker<System.Drawing.Bitmap> * CancellationToken -> Task<QrResult>
type CredentialDelegateType = ApiClient.AuthPost * CancellationToken -> Task<Result<ApiClient.VerificationResult,CameraControl.ApiResult>>

let getImage (qrControl:CameraControl.CameraControl) : ImageDelegateType =
    fun ct ->
        if not ct.IsCancellationRequested then
            None
        else
            match qrControl.TakeSnap() with
            | Ok bm ->
                printfn $"GotImage:{bm.GetText()} -IsDisposed?{bm.IsDisposed}"
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
                match dispBm.TryGet("tryGetQrCode") with
                | None -> return QrResult.QrNotFound
                | Some bm ->
                    return qrManager.TryDecode (bm,ct)
        }
let verifyQrCode config : CredentialDelegateType =
    fun (ap,ct) ->
        task {
            return CameraControl.UI.verifyQrCode config (ap.Code,ct)
        }
let createVerifyQrCodeDelegate: _ -> CredentialDelegateType =
    fun config ->
        verifyQrCode config

type WorkerState =
    | NotStarted
    | Started
    | Paused
    | Stopped

type Worker(checkHandle:System.Func<bool>, getImage: ImageDelegateType, tryGetQr: QrDelegateType, tryGetCredentials: CredentialDelegateType, onFound: ApiClient.VerificationResult -> unit, cts: System.Threading.CancellationTokenSource) =

    let l = obj()

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
            while state <> Started do
                do! Task.Delay(150, cts.Token)
            printfn "Started!"
            while not cts.IsCancellationRequested && checkHandle.Invoke() do
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
