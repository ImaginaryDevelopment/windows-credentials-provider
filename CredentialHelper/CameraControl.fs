module CredentialHelper.CameraControl

open BReusable
open BReusable.Controls

open System
open System.Collections.Generic
open System.ComponentModel
open System.Data
open System.Drawing
open System.Drawing.Imaging
open System.Linq
open System.Text
open System.Threading
open System.Threading.Tasks
open System.Windows.Forms
open OpenCvSharp

//open OpenCvSharp.CPlusPlus
open OpenCvSharp.Extensions

// https://github.com/lavahasif/camera/blob/master/camera/Form1.cs
// https://github.com/lavahasif/camera/blob/master/camera/Form1.Designer.cs

// https://stackoverflow.com/questions/17169757/what-is-f-equivalent-of-c-sharp-public-event
type OnCredentialSubmitHandler = delegate of System.Net.NetworkCredential -> unit

type ProtectedValue<'t> (value: 't, fValidate: unit -> bool) =
    let mutable value = value

    //new(value, fValidate: Func<bool>) = ProtectedValue<'t>(value, fValidate.Invoke)
    static member public CCreate(value, fValidate: Func<bool>) = ProtectedValue<'t>(value, fValidate.Invoke)
    member this.Value
        with get() =
            value

    member _.TrySetValue(next:'t) =
        if fValidate() then
            value <- next
            true
        else false

[<Struct>]
type CameraState =
    | Stopped
    | Initializing
    | Started
    with
        // https://stackoverflow.com/questions/4168220/implement-c-sharp-equality-operator-from-f
        static member op_Equality(left:CameraState, right: CameraState) =
            left = right

//let (|NullString|EmptyString|WhiteSpace|ValueString|) (value:string) =
//    match value with
//    | null -> NullString
//    | x when String.IsNullOrEmpty x -> EmptyString
//    | x when String.IsNullOrWhiteSpace x -> WhiteSpace x
//    | x -> ValueString x

//let tryGetValueString value =
//    match value with
//    | NullString -> None
//    | EmptyString -> None
//    | WhiteSpace _ -> None
//    | ValueString x -> Some x

//let (|IsUndisposed|IsDisposed|) (x:DisposableCvObject) =
//    match x with
//    | null -> IsDisposed
//    | _ when x.IsDisposed -> IsDisposed
//    | x -> IsUndisposed x

//let iConsumeDisposableCv x f =
//    match x with
//    | IsDisposed -> ()
//    | IsUndisposed x -> f x

//let runOnValue foo = iConsumeDisposableCv foo

//let mydelegate = runOnValue Unchecked.defaultof<_>

// isolate startup and shutdown
type CaptureWrapper () =

    let mutable capture : VideoCapture = null

    let tryGetCapture () =
        match capture with
        | null -> None
        | _ when capture.IsDisposed -> None
        | _ -> Some capture

    member this.TryStart(cameraIndex:int) =
        this.CleanUp()
        capture <- new VideoCapture(cameraIndex)
        capture.Open cameraIndex |> ignore<bool>
        capture.IsOpened()

    member _.IsOpened =
        tryGetCapture()
        |> Option.map(_.IsOpened())
        |> Option.defaultValue false

    member _.CleanUp () =
        tryGetCapture()
        |> Option.iter(fun _ ->
            let inline tryF title f =
                try
                    f()
                with ex ->
                    eprintfn "capture %s error: %s" title ex.Message
            // release will throw if already disposed
            // https://github.com/shimat/opencvsharp/blob/728293a3631386af5edafccaa9ba31ceeec9e0a6/src/OpenCvSharp/Modules/videoio/VideoCapture.cs#L1112
            tryF "release" (fun () -> capture.Release())
            tryF "dispose" (fun () -> capture.Dispose())
            capture <- null
        )

    member _.Read (frame:Mat) =
        tryGetCapture()
        |> Option.filter(_.IsOpened())
        |> Option.map(fun capture ->

            capture.Read(frame)
        )
        |> Option.defaultValue false

    member this.Dispose() = this.CleanUp()

    interface IDisposable with
        member this.Dispose() = this.Dispose()

type CameraControl(imageProp: Property<Image>, sleepFetchOpt: unit -> int option, logger: LogDelegate) =

    let captureWrapper = new CaptureWrapper()
    // hold onto image so it can be disposed
    let mutable image: DisposalTracker<Bitmap> option = None
    let mutable frame: DisposalTracker<Mat> option = None
    let mutable cameraThread : Thread = null // Thread(ThreadStart(this.capturecameracallback))
    let mutable cameraState = ObservableStore(CameraState.Stopped)

    //member val OnStateChange: (CameraState*CameraState -> unit) option = None

    member _.CameraState = cameraState
    // hide details
    member _.IsRunning = cameraState.Value = CameraState.Started && captureWrapper.IsOpened

    member _.CaptureCamera (index: int, ct: System.Threading.CancellationToken) =
        let onStarted index =
            logger.Invoke($"captureWrapper starting on %i{index}", None)
            while not ct.IsCancellationRequested && cameraState.Value <> Stopped do
                match frame with
                | Some frame when frame.IsDisposed ->
                    None
                | None -> None
                | Some frame -> Some frame
                |> Option.defaultWith(fun () ->
                    let frameValue = new DisposalTracker<_>("MatFrame", new Mat(), false)
                    frame <- Some frameValue
                    frameValue
                )
                |> fun frame -> frame.TryGet()
                |> Option.iter (fun frame ->
                    if not <| captureWrapper.Read frame then
                        let msg = $"Camera read failed: '%A{cameraState.Value}'" 
                        eprintfn "%s" msg
                        logger.Invoke(msg, Some EventLogType.FailureAudit)
                    else
                    cameraState.Value <- Started
                    // get the old image to dispose later
                    // should we be using the local `image` field instead of a getter?
                    let toDispose = image // imageProp.Getter()
                    // store the image locally
                    let bm = BitmapConverter.ToBitmap frame
                    // https://stackoverflow.com/questions/9758403/flipping-an-image-horizontally-in-opencvsharp
                    bm.RotateFlip(RotateFlipType.Rotate180FlipY)
                    image <- Some (new DisposalTracker<_>("BitFrame", bm, false))
                    // set the image into the parent prop
                    imageProp.Setter bm
                    // dispose the old image
                    toDispose
                    |> Option.iter(fun image ->
                        if not image.IsDisposed then
                            image.Dispose("cameraCaptureCallback")
                    )
                )
                match sleepFetchOpt() with
                | None -> ()
                | Some sleep -> System.Threading.Thread.Sleep(sleep)
            ()
        match cameraState.Value with
        | Started
        | Initializing -> ()
        | Stopped ->
            // TODO: consider performance of running in too tight of a loop
            let captureCameraCallback =
                fun () ->
                    if not ct.IsCancellationRequested then
                        try
                            let toTry = [index;index+1;-1]
                            cameraState.Value <- Initializing
                            // should we reuse this one?
                            toTry
                            |> Seq.tryFind(fun index ->
                                if ct.IsCancellationRequested then
                                    false
                                else
                                    let worked = captureWrapper.TryStart index
                                    if not worked then
                                        let msg = $"captureWrapper failed to start on %i{index}" 
                                        eprintfn "%s" msg
                                        logger.Invoke(msg, Some EventLogType.Error)
                                    worked
                            )
                            |> Option.iter onStarted
                        with ex ->
                            if not ct.IsCancellationRequested then
                                logger.Invoke("captureCameraCallback failed:" + tryGetTypeName ex + ":" + ex.Message, Some EventLogType.Error)


            match cameraThread with
            | null -> ()
            | _ -> cameraThread.Abort()
            if not ct.IsCancellationRequested then
                cameraThread <- Thread(ThreadStart captureCameraCallback)
                cameraThread.Name <- "Camera reader"
                if not ct.IsCancellationRequested then
                    cameraThread.Start()

    member _.StopCapture () =
        match cameraState.Value with
        | Stopped -> ()
        | _ ->
            cameraState.Value <- Stopped
            captureWrapper.CleanUp()

    member _.TakeSnap () =
        match cameraState.Value with
        | CameraState.Started ->
            match image with
            | None -> Error "No image"
            | Some image -> Ok image

        | CameraState.Stopped ->
            let msg = "Cannot take picture if the camera isn't capturing images"
            printfn $"{msg}"
            Error msg
        | CameraState.Initializing ->
            Error "Camera is starting up"

    member _.Dispose() =
        cameraThread
        |> Option.ofObj
        |> Option.iter(fun _ ->
            cameraState.Value <- Stopped
            if cameraThread.IsAlive then
                try
                    cameraThread.Abort()
                with _ -> ()
        )
        let disposals : (string * IDisposable option) list =
            [
                nameof captureWrapper, Some captureWrapper // |> Option.ofObj |> Option.map toDisposable
                //nameof image, image |> Option.bind (fun image -> image.TryGet() |> Option.map toDisposable) // |> Option.ofObj |> Option.map toDisposable
                nameof image, image |> DisposalTracker.tryGetNamedDisposable "CameraControlDispose"
                nameof frame, frame  |> DisposalTracker.tryGetNamedDisposable "CameraControlDispose"
            ]
        disposals
        |> List.choose Option.ofSnd
        |> List.choose(fun (n,v) ->
            match v with
            | null -> None
            | _ -> Some (n,v)
        )
        |> List.iter (uncurry tryDispose)
        ()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

type ApiResult =
    | ApiUrlError of string
    | ApiValidationFailed of string
    | ApiResponseReadError of string
    | Cancelled
    with
        member this.TryGetError () =
            match this with
            | ApiUrlError v -> v
            | ApiValidationFailed v -> v
            | ApiResponseReadError v -> v
            | Cancelled -> nameof Cancelled

type SnapshotResult =
    | InvalidCameraState
    | SnapError of string
    with
        member this.TryGetError () =
            match this with
            | SnapError v -> v
            | InvalidCameraState -> nameof InvalidCameraState

module UI =

    let inline add x y = x + y
    let blah () =
        add "test" "test 2" |> ignore
        add 1 2 |> ignore

    let mutable onceInitialized = None

    let cleanPictureBox title next (pb: PictureBox, ct: System.Threading.CancellationToken) =
        try
            pb.Image |> Option.ofObj |> Some
        with ex ->
            eprintfn "Attempt to read pb Image failed:%s" title
            None
        |> Option.iter(
            function
            | None -> ()
            | Some x ->
                // clear the image from the picture box
                let f () =
                    if not ct.IsCancellationRequested then
                        pb.Image <- next
                if not ct.IsCancellationRequested then
                    try
                        pb |> ensureInvoke f |> ignore<obj>
                    with
                        // happens if the form is closed when we try to update the picturebox
                        | :? System.ComponentModel.InvalidAsynchronousStateException ->
                            eprintfn "Attempt to update invalid UI"
                            eprintfn "Why?"
                            ()

                // dispose the image
                try
                    x.Dispose()
                with ex ->
                    eprintfn "Failed to dispose image: '%s'" ex.Message
        )


    let createCameraControl (pb:PictureBox, sleepFetchOpt: Func<int option>, logger:LogDelegate, ct: System.Threading.CancellationToken) =
        let mutable cc : CameraControl option = None
        let setter v =
            if ct.IsCancellationRequested then
                ()
            else
                cc
                |> function
                    | None ->
                        let msg = "Setter called without camera control"
                        eprintfn "%s" msg
                        logger.Invoke(msg, Some EventLogType.Error)
                    | Some cc ->
                        if cc.CameraState.Value = CameraState.Started then
                            onceInitialized
                            |> Option.iter(fun f ->
                                f()
                            )

                cleanPictureBox "createCameraControl" v (pb,ct)
                let f() = pb.Image <- v
                pb |> ensureInvoke f |> ignore<obj>

        let getter () =
            let mutable image: Image = null
            if ct.IsCancellationRequested then
                image
            else
                let f() = image <- pb.Image
                pb |> ensureInvoke f |> ignore<obj>
                image

        let imControl = new CameraControl({Getter=getter; Setter= setter}, (fun () ->  sleepFetchOpt.Invoke()), logger )
        cc <- Some imControl
        imControl

    let getRunText =
        function
        | CameraState.Stopped -> "Start"
        | CameraState.Initializing -> nameof CameraState.Initializing
        | CameraState.Started -> "Stop"

    let inline onRunRequest (imControl:CameraControl,cameraIndex:ProtectedValue<int>,pb,ct) = 
        match imControl.CameraState.Value with
        | Initializing -> ()
        | Stopped ->
            imControl.CaptureCamera (cameraIndex.Value,ct)

        | CameraState.Started ->
            imControl.StopCapture()
            cleanPictureBox "onRunRequest" null (pb,ct)
        //setRunText imControl.CameraState.Value runButton

    let setRunText cs (runButton:Button) =
        let value = getRunText cs
        setTextIfNot runButton value

    let verifyQrCode (config:AppSettings.AppConfig) (qrResult,ct: CancellationToken) =
        let url = config.DevApi
        match ApiClient.BaseUrl.TryCreate url with
        | Error e ->
            Error <| ApiUrlError $"{qrResult}:devapi:'{url}':{e}"
        | Ok baseUrl ->
            //printfn "About to show msg box"
            //showMsgBox qrResult
            //printfn "Yay we made it"
            if not ct.IsCancellationRequested then
                let t = ApiClient.tryValidate config baseUrl ({ Code = qrResult}, ct)
                t
                |> Async.AwaitTask
                |> Async.Catch
                |> Async.RunSynchronously
                |> function 
                    | Choice1Of2(Ok v) -> Ok v
                    | Choice1Of2(Error e) -> Error (ApiClient.ApiResultError.ToErrorMessage e)
                    | Choice2Of2 e ->
                        let str = formatException e
                        Error $"{str}:{e.StackTrace}"
                |> function
                    | Error e -> Error(ApiValidationFailed e)
                    | Ok v ->
                        PackageAdapters.Cereal.tryDeserialize<ApiClient.VerificationResult> v
                        |> Result.mapError(fun (txt,ex) ->
                            ApiResponseReadError $"{ex.Message}:'{txt}'"
                        )
            else
                Error ApiResult.Cancelled

    let onSnapRequest (imControl:CameraControl) =

        match imControl.CameraState.Value with
        | Initializing
        | Stopped -> Error InvalidCameraState
        | Started ->
            printfn "Taking snap"
            imControl.TakeSnap()
            |> function
                | Error msg -> Error(SnapError msg)
                | Ok snapshot -> Ok snapshot

    let onQrVerifyRequest (qrControl:QRCode.QrManager) snapshot =
        qrControl.TryDecode snapshot

    let onVerifyRequest qrCode =
                    verifyQrCode qrCode

    let onCameraIndexChangeRequest (cameraIndexComboBox: ComboBox, cameraIndex: ProtectedValue<int>, imControl:CameraControl, pb:PictureBox, ct: System.Threading.CancellationToken) =
        if ct.IsCancellationRequested then
            ()
        else
            match cameraIndexComboBox.SelectedItem |> Option.ofObj |> Option.defaultWith(fun () -> cameraIndexComboBox.Text) with
            | :? int as i -> Some i
            | :? string as s -> s |> String.trim |> tryParseInt
            | _ -> None
            |> Option.teeNone(fun _ ->
                    eprintfn "Could not read comboBox1 value: '%A'" cameraIndexComboBox.SelectedValue
            )
            |> Option.bind(fun i -> cameraIndex.TrySetValue i |> Option.ofFalse)
            |> Option.iter(fun _ ->
                eprintfn "Failed to set camera index: '%A' - '%A'" cameraIndexComboBox.SelectedItem cameraIndexComboBox.SelectedText
                if imControl.IsRunning || imControl.CameraState.Value <> CameraState.Stopped then
                    // set the comboBox back to the value of the current camera index
                    cameraIndexComboBox.Items
                    |> Seq.cast<int>
                    |> Seq.tryFindIndex(fun v -> v = cameraIndex.Value)
                    |> function
                        | Some i ->
                            printfn "Setting combobox index to %i" i
                            cleanPictureBox "onCameraIndexChangeRequest" null (pb,ct)
                            cameraIndexComboBox.SelectedIndex <- i
                        | None ->
                            printfn "Changing combobox text to %i" cameraIndex.Value
                            cameraIndexComboBox.SelectedValue <- cameraIndex.Value
                            //comboBox1.SelectedText <- string cameraIndex.Value
            )

    let hookUpCameraStateChanges(imControl:CameraControl, runButton:Button, snapButton:Button, cameraIndexComboBox: ComboBox) =
        imControl.CameraState.Subscribe(fun value ->

            let inline setRunButtonTextIfNot text = setTextIfNot runButton text

            let inline setRunButtonEnabledIfNot enabled = setEnabledIfNot runButton enabled

            let setSnapButtonIfNot enabled = setEnabledIfNot snapButton enabled

            let setCameraIndexComboEnabled enabled = setEnabledIfNot cameraIndexComboBox enabled

            //button1.Enabled <- imControl.CameraState.Value <> CameraState.Initializing
            // disallow state changes attempts while initializing
            setRunButtonEnabledIfNot (value <> CameraState.Initializing)

            //button2.Enabled <- imControl.CameraState.Value = CameraState.Started
            // disallow snaps while not running
            setSnapButtonIfNot (value = CameraState.Started && imControl.IsRunning)
            setCameraIndexComboEnabled (value = CameraState.Stopped)

            match value with
            | CameraState.Stopped -> "Start"
            | CameraState.Initializing -> nameof CameraState.Initializing
            | CameraState.Started -> "Stop"
            |> setRunButtonTextIfNot
        )

