module CredentialHelper.CameraControl

open Reusable
open Reusable.Controls

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
    member this.Value
        with get() =
            value

    member _.TrySetValue(next:'t) =
        if fValidate() then
            value <- next
            true
        else false

type CameraState =
    | Stopped
    | Initializing
    | Started

// isolate startup and shutdown
type CaptureWrapper () =

    let mutable capture : VideoCapture = null
    let tryGetCapture() =
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

type CameraControl(imageProp: Property<Image>) =

    let captureWrapper = new CaptureWrapper()
    // hold onto image so it can be disposed
    let mutable image: Bitmap = null
    let mutable cameraThread : Thread = null // Thread(ThreadStart(this.capturecameracallback))
    let mutable cameraState = ObservableStore(CameraState.Stopped)

    //member val OnStateChange: (CameraState*CameraState -> unit) option = None

    member _.CameraState = cameraState
    // hide details
    member _.IsRunning = cameraState.Value = CameraState.Started && captureWrapper.IsOpened

    member _.CaptureCamera (index: int) =
        match cameraState.Value with
        | Started
        | Initializing -> ()
        | Stopped ->
            // TODO: consider performance of running in too tight of a loop
            let captureCameraCallback =
                let mutable frame: Mat = null
                fun () ->
                    cameraState.Value <- Initializing
                    frame <- new Mat()
                    if captureWrapper.TryStart index then
                        while cameraState.Value <> Stopped do
                            if captureWrapper.Read frame then
                                cameraState.Value <- Started
                                image <- BitmapConverter.ToBitmap frame
                                imageProp.Getter()
                                |> Option.ofObj
                                |> Option.iter(fun image ->
                                    image.Dispose()
                                )
                                imageProp.Setter image
                            else
                                eprintfn "Camera read failed: '%A'" cameraState.Value
                    else
                        eprintfn "captureWrapper failed to start"
                    ()
            match cameraThread with
            | null -> ()
            | _ -> cameraThread.Abort()
            cameraThread <- Thread(ThreadStart captureCameraCallback)
            cameraThread.Name <- "Camera reader"
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
            match imageProp.Getter() with
            | null -> Error "image getter returned null"
            | image ->
                try
                    let snapshot = new Bitmap(image)
                    Ok snapshot
                with
                    | :? ArgumentException as ex ->
                        Error ex.Message

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
        let disposals : (string * IDisposable) list =
            [
                nameof captureWrapper, captureWrapper // |> Option.ofObj |> Option.map toDisposable
                nameof image, image // |> Option.ofObj |> Option.map toDisposable
            ]
        disposals
        |> List.choose(fun (n,v) ->
            match v with
            | null -> None
            | _ -> Some (n,v)
        )
        |> List.iter (uncurry tryDispose)
        ()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

// TODO: when there is no camera available the ui does not indicate anything is wrong
type Form1() as this =
    inherit System.Windows.Forms.Form()

    let onCredentialSubmitEvent = DelegateEvent<OnCredentialSubmitHandler>()
    let onCancelEvent = DelegateEvent<OnAction>()
    let onOkEvent = DelegateEvent<OnAction>()
    //let onFormClosed = DelegateEvent<OnAction>()

    let pictureBox1: PictureBox =
        new PictureBox(
            Location=System.Drawing.Point(22,13),
            Name="pictureBox1",
            Size=System.Drawing.Size(798,472),
            TabIndex=0,
            TabStop=false
        )

    let cleanPictureBox () =
        match pictureBox1.Image with
        | null -> ()
        | x ->
            try
                x.Dispose()
            with ex ->
                eprintfn "Failed to dispose image: '%s'" ex.Message
            let f () =
                pictureBox1.Image <- null
            pictureBox1 |> ensureInvoke f |> ignore<obj>

    let mutable onceInitialized = None
    let imControl =
        let mutable cc : CameraControl option = None
        let setter v =
            cc
            |> function
                | None -> eprintfn "Setter called without camera control"
                | Some cc ->
                    if cc.CameraState.Value = CameraState.Started then
                        onceInitialized
                        |> Option.iter(fun f ->
                            f()
                        )
            cleanPictureBox()
            let f() = pictureBox1.Image <- v
            pictureBox1 |> ensureInvoke f |> ignore<obj>

        let imControl = new CameraControl({Getter=(fun() -> pictureBox1.Image); Setter= setter})
        cc <- Some imControl
        imControl

    // this should not be changed if the camera is running
    let cameraIndex = ProtectedValue(0, fun () -> imControl.CameraState.Value = CameraState.Stopped && not imControl.IsRunning)

    let qrControl = QRCode.QrManager()

    let generateDefaultPath () = System.String.Format(@"image-{0}.jpg", Guid.NewGuid())
    let controlTop = 491

    let cameraIndexComboBox = new ComboBox(
        Location=new System.Drawing.Point(10, controlTop),
        Name="comboBox1",
        Size= new System.Drawing.Size(100,50),
        TabIndex=1,
        Visible=true,
        Enabled=true
    )

    let getRunText =
        function
        | CameraState.Stopped -> "Start"
        | CameraState.Initializing -> nameof CameraState.Initializing
        | CameraState.Started -> "Stop"

    let setRunText cs (runButton:Button) =
        let value = getRunText cs
        setTextIfNot runButton value

    let runButton = new Button(
        Name = "runButton",
        Location = new System.Drawing.Point(cameraIndexComboBox.Location.X + cameraIndexComboBox.Size.Width + 15, controlTop),
        Size = new System.Drawing.Size(144, 52),
        TabIndex = 1,
        Text = (imControl.CameraState.Value |> getRunText),
        UseVisualStyleBackColor = true,
        Visible = true,
        Enabled = false
    )

    let snapButton = new Button(
       Name = "snapButton",
       Location = new System.Drawing.Point(runButton.Location.X + runButton.Size.Width + 15, controlTop),
       Font = new System.Drawing.Font("Microsoft YaHei UI", 16.2f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0uy),
       Size = new System.Drawing.Size(259, 86),
       TabIndex = 2,
       Text = "scan",
       Enabled = false,
       UseVisualStyleBackColor = true
    )

    let runButtonClick _ _ =
        match imControl.CameraState.Value with
        | Initializing -> ()
        | Stopped ->
            imControl.CaptureCamera cameraIndex.Value

        | CameraState.Started ->
            imControl.StopCapture()
            cleanPictureBox()
        setRunText imControl.CameraState.Value runButton


    let snapButtonClick _ _ =
        match imControl.CameraState.Value with
        | Initializing -> ()
        | Stopped -> ()
        | Started ->
            imControl.TakeSnap()
            |> function
                | Error msg -> System.Windows.Forms.MessageBox.Show msg |> ignore
                | Ok snapshot ->
                //generateDefaultPath()
                //|> snapshot.Save
                qrControl.TryDecode snapshot
                |> Option.iter(fun qrResult ->
                    System.Windows.Forms.MessageBox.Show qrResult |> ignore
                    //button2.Text <- qrResult
                )

    let comboBox1Change =
        // combo box may try to set its own value
        let mutable cbLatch = false
        fun _ _ ->
            if not cbLatch then
                cbLatch <- true
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
                                cleanPictureBox()
                                cameraIndexComboBox.SelectedIndex <- i
                            | None ->
                                printfn "Changing combobox text to %i" cameraIndex.Value
                                cameraIndexComboBox.SelectedValue <- cameraIndex.Value
                                //comboBox1.SelectedText <- string cameraIndex.Value
                )
                cbLatch <- false

    let components : System.ComponentModel.IContainer = null
    let mutable disposables : (string * System.IDisposable) list = List.empty
    let addDisposable title x =
        disposables <-
            (title,x)
            |> List.singleton
            |> List.append disposables

    do
        this.InitializeComponent()

        imControl.CameraState.Subscribe(fun value ->

            let isStopped = value = CameraState.Stopped

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
        |> addDisposable "combobox camerastate"

        // there is a startup time to grabbing the camera and starting to display it on the screen

        // relies on capture camera invoking the setter above to kick off post-initializing work
        imControl.CaptureCamera(cameraIndex.Value)

    override _.OnClosed e =
        base.OnClosed e
        try
            imControl.Dispose()
        with _ -> ()

    member private this.InitializeComponent() =
        this.SuspendLayout()

        // this.button1.Click += new System.EventHandler(this.button1_Click);
        System.EventHandler runButtonClick |> runButton.Click.AddHandler
        // this.button2.Click += new System.EventHandler(this.button2_Click);
        System.EventHandler snapButtonClick |> snapButton.Click.AddHandler
        System.EventHandler comboBox1Change |> cameraIndexComboBox.SelectedValueChanged.AddHandler

        //
        // Form1
        //

        this.AutoScaleDimensions <- new System.Drawing.SizeF(8f, 16f)
        this.AutoScaleMode <- AutoScaleMode.Font
        this.ClientSize <- System.Drawing.Size(832,585)

        // TODO: detect available camera indexes
        [0..3]
        |> Seq.iter (cameraIndexComboBox.Items.Add>> ignore<int>)

        this.Controls.Add cameraIndexComboBox
        this.Controls.Add snapButton
        this.Controls.Add runButton
        this.Controls.Add pictureBox1

        this.Name <- "Form1"
        this.Text <- "Take Snapshot"

        // this.Load += new System.EventHandler(this.Form1_Load_1);
        //this.Load.Add |> ignore

        (pictureBox1 :> System.ComponentModel.ISupportInitialize).EndInit()
        this.ResumeLayout(false)

    override this.Dispose(disposing) =
            if disposing && not <| isNull components then
                components.Dispose()
            disposables
            |> List.iter (uncurry tryDispose)
            base.Dispose disposing

    [<CLIEvent>]
    member _.OnCredentialSubmit = onCredentialSubmitEvent.Publish
    [<CLIEvent>]
    member _.OnCancelEvent = onCancelEvent.Publish
    [<CLIEvent>]
    member _.OnOkEvent = onOkEvent.Publish
    //[<CLIEvent>]
    //member _.OnFormClosed = onFormClosed.Publish
