module CredentialHelper.CameraControl

open Reusable

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
type OnAction = delegate of unit -> unit

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
    let mutable cameraState = Stopped

    member _.CameraState = cameraState
    // hide details
    member _.IsRunning = cameraState = CameraState.Started && captureWrapper.IsOpened

    member _.CaptureCamera (index: int) =
        match cameraState with
        | Started
        | Initializing -> ()
        | Stopped ->
            // TODO: consider performance of running in too tight of a loop
            let captureCameraCallback =
                let mutable frame: Mat = null
                fun () ->
                    cameraState <- Initializing
                    frame <- new Mat()
                    if captureWrapper.TryStart index then
                        while cameraState <> Stopped do
                            if captureWrapper.Read frame then
                                cameraState <- Started
                                image <- BitmapConverter.ToBitmap frame
                                imageProp.Getter()
                                |> Option.ofObj
                                |> Option.iter(fun image ->
                                    image.Dispose()
                                )
                                imageProp.Setter image
                            else
                                eprintfn "Camera read failed"
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
        match cameraState with
        | Stopped -> ()
        | _ ->
            cameraState <- Stopped
            captureWrapper.CleanUp()

    member _.TakeSnap () =
        match cameraState with
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
            cameraState <- Stopped
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

    let mutable onceInitialized = None
    let imControl =
        let mutable cc : CameraControl option = None
        let setter v =
            cc
            |> function
                | None -> eprintfn "Setter called without camera control"
                | Some cc ->
                    if cc.CameraState = CameraState.Started then
                        onceInitialized
                        |> Option.iter(fun f ->
                            f()
                        )
            pictureBox1.Image <- v
        let imControl = new CameraControl({Getter=(fun() -> pictureBox1.Image); Setter= setter})
        cc <- Some imControl
        imControl

    // this should not be changed if the camera is running
    let cameraIndex = ProtectedValue(0, fun () -> imControl.CameraState = CameraState.Stopped && not imControl.IsRunning)

    let qrControl = QRCode.QrManager()

    let generateDefaultPath () = System.String.Format(@"image-{0}.jpg", Guid.NewGuid())
    let controlTop = 491

    let comboBox1 = new ComboBox(
        Location=new System.Drawing.Point(10, controlTop),
        Name="comboBox1",
        Size= new System.Drawing.Size(100,50),
        TabIndex=1,
        Visible=true,
        Enabled=true
    )

    let button1 = new Button(
        Location = new System.Drawing.Point(comboBox1.Location.X + comboBox1.Size.Width + 15, controlTop),
        Name = "button1",
        Size = new System.Drawing.Size(144, 52),
        TabIndex = 1,
        Text = "Initializing",
        UseVisualStyleBackColor = true,
        Visible = true,
        Enabled = false
    )

    let button2 = new Button(
       Font = new System.Drawing.Font("Microsoft YaHei UI", 16.2f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0uy),
       Location = new System.Drawing.Point(button1.Location.X + button1.Size.Width + 15, controlTop),
       Name = "button2",
       Size = new System.Drawing.Size(459, 86),
       TabIndex = 1,
       Text = "scan",
       Enabled = false,
       UseVisualStyleBackColor = true
    )

    let button1Click _ _ =
        match imControl.CameraState with
        | Initializing -> ()
        | Stopped ->
            imControl.CaptureCamera(cameraIndex.Value)
            button1.Text <- "Stop"
            button2.Text <- "scan"
        | CameraState.Started ->
            imControl.StopCapture()
            button1.Text <- "Start"

    let button2Click _ _ =
        match imControl.CameraState with
        | Initializing -> ()
        | Stopped -> ()
        | Started ->
            imControl.TakeSnap()
            |> function
                | Error msg -> System.Windows.Forms.MessageBox.Show(msg) |> ignore
                | Ok snapshot ->
                //generateDefaultPath()
                //|> snapshot.Save
                qrControl.TryDecode(snapshot)
                |> Option.iter(fun qrResult ->
                    System.Windows.Forms.MessageBox.Show(qrResult) |> ignore
                    //button2.Text <- qrResult
                )
    let comboBox1Change =
        // combo box may try to set its own value
        let mutable cbLatch = false
        fun _ _ ->
            if not cbLatch then
                cbLatch <- true
                match comboBox1.SelectedItem |> Option.ofObj |> Option.defaultWith(fun () -> comboBox1.Text) with
                | :? int as i -> Some i
                | :? string as s -> s |> String.trim |> tryParseInt
                | _ -> None
                |> Option.teeNone(fun _ ->
                        eprintfn "Could not read comboBox1 value: '%A'" comboBox1.SelectedValue
                )
                |> Option.bind(fun i -> cameraIndex.TrySetValue i |> Option.ofFalse)
                |> Option.iter(fun _ ->
                    eprintfn "Failed to set camera index: '%A' - '%A'" comboBox1.SelectedItem comboBox1.SelectedText
                    if imControl.IsRunning || imControl.CameraState <> CameraState.Stopped then
                        // set the comboBox back to the value of the current camera index
                        comboBox1.Items
                        |> Seq.cast<int>
                        |> Seq.tryFindIndex(fun v -> v = cameraIndex.Value)
                        |> function
                            | Some i ->
                                printfn "Setting combobox index to %i" i
                                comboBox1.SelectedIndex <- i
                            | None ->
                                printfn "Changing combobox text to %i" cameraIndex.Value
                                comboBox1.SelectedValue <- cameraIndex.Value
                                //comboBox1.SelectedText <- string cameraIndex.Value
                )
                cbLatch <- false

    let components : System.ComponentModel.IContainer = null

    do
        this.InitializeComponent()
        // there is a startup time to grabbing the camera and starting to display it on the screen

        // relies on capture camera invoking the setter above to kick off post-initializing work
        onceInitialized <- Some <| fun _ ->
            let updatebutton: OnAction =
                OnAction(fun _ ->
                    button1.Text <- "Stop"
                    button1.Enabled <- imControl.CameraState <> CameraState.Initializing
                    button2.Enabled <- imControl.CameraState = CameraState.Started
                    )
            button1.Invoke updatebutton |> ignore
        imControl.CaptureCamera(cameraIndex.Value)


    override this.OnClosed e =
        base.OnClosed e
        try
            imControl.Dispose()
        with _ -> ()

    member private this.InitializeComponent() =
        this.SuspendLayout()

        // this.button1.Click += new System.EventHandler(this.button1_Click);
        System.EventHandler button1Click |> button1.Click.AddHandler
        // this.button2.Click += new System.EventHandler(this.button2_Click);
        System.EventHandler button2Click |> button2.Click.AddHandler
        System.EventHandler comboBox1Change |> comboBox1.SelectedValueChanged.AddHandler

        //
        // Form1
        //

        this.AutoScaleDimensions <- new System.Drawing.SizeF(8f, 16f)
        this.AutoScaleMode <- AutoScaleMode.Font
        this.ClientSize <- System.Drawing.Size(832,585)

        // TODO: detect available camera indexes
        [0..3]
        |> Seq.iter (comboBox1.Items.Add>> ignore<int>)

        this.Controls.Add comboBox1
        this.Controls.Add button2
        this.Controls.Add button1
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
            base.Dispose disposing

    [<CLIEvent>]
    member _.OnCredentialSubmit = onCredentialSubmitEvent.Publish
    [<CLIEvent>]
    member _.OnCancelEvent = onCancelEvent.Publish
    [<CLIEvent>]
    member _.OnOkEvent = onOkEvent.Publish
    //[<CLIEvent>]
    //member _.OnFormClosed = onFormClosed.Publish
