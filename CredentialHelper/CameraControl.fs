module CredentialHelper.CameraControl

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

// TODO: when there is no camera available the ui does not indicate anything is wrong
type Form1() as this =
    inherit System.Windows.Forms.Form()

    let onCredentialSubmitEvent = DelegateEvent<OnCredentialSubmitHandler>()
    let onCancelEvent = DelegateEvent<OnAction>()
    let onOkEvent = DelegateEvent<OnAction>()
    //let onFormClosed = DelegateEvent<OnAction>()

    let mutable index = 0

    let mutable capture : VideoCapture = null
    let mutable image: Bitmap = null
    let mutable camera : Thread = null // Thread(ThreadStart(this.capturecameracallback))

    let mutable isCameraRunning = false

    let pictureBox1: PictureBox =
        new PictureBox(
            Location=System.Drawing.Point(22,13),
            Name="pictureBox1",
            Size=System.Drawing.Size(798,472),
            TabIndex=0,
            TabStop=false
        )

    let button1 = new Button(
        Location = new System.Drawing.Point(65, 478),
        Name = "button1",
        Size = new System.Drawing.Size(144, 52),
        TabIndex = 1,
        Text = "Start",
        UseVisualStyleBackColor = true,
        Visible = false
    )

    let button2 = new Button(
       Font = new System.Drawing.Font("Microsoft YaHei UI", 16.2f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0uy),
       Location = new System.Drawing.Point(215, 491),
       Name = "button2",
       Size = new System.Drawing.Size(459, 86),
       TabIndex = 1,
       Text = "save",
       UseVisualStyleBackColor = true
    )

    let captureCameraCallback =
        let mutable frame: Mat = null
        fun () ->
            frame <- new Mat()
            capture <- new VideoCapture(index)
            capture.Open(index) |> ignore<bool>
            if capture.IsOpened() then
                while isCameraRunning do
                    capture.Read frame |> ignore<bool>
                    image <- BitmapConverter.ToBitmap frame
                    match pictureBox1.Image with
                    | null -> ()
                    | image -> image.Dispose()
                    pictureBox1.Image <- image
            ()

    let captureCamera () =
        match camera with
        | null -> ()
        | _ -> camera.Abort()
        camera <- Thread(ThreadStart(captureCameraCallback))
        camera.Start()
        ()

    let button1Click _ _ =
                if button1.Text = "Start" then
                    captureCamera()
                    button1.Text <- "Stop"
                    isCameraRunning <- true
                else
                    capture.Release()
                    button1.Text <- "Start"
                    isCameraRunning <- false

    let button2Click _ _ =
        if isCameraRunning then
            let snapshot = new Bitmap(pictureBox1.Image)
            snapshot.Save(System.String.Format(@"image-{0}.jpg", Guid.NewGuid()))
            capture.Release()
            camera.Abort()
            // this.Close()
        else
            printfn "Cannot take picture if the camera isn't capturing images"
        ()

    let components : System.ComponentModel.IContainer = null

    do
        this.InitializeComponent()
        captureCamera()
        button1.Text <- "Stop"
        isCameraRunning <- true


    override this.OnClosed e =
        base.OnClosed e
        try
            capture.Release()
            camera.Abort()
        with _ -> ()

    member private this.InitializeComponent() =
        this.SuspendLayout()

        // this.button1.Click += new System.EventHandler(this.button1_Click);
        System.EventHandler button1Click |> button1.Click.AddHandler
        // this.button2.Click += new System.EventHandler(this.button2_Click);
        System.EventHandler button2Click |> button2.Click.AddHandler

        //
        // Form1
        //

        this.AutoScaleDimensions <- new System.Drawing.SizeF(8f, 16f)
        this.AutoScaleMode <- AutoScaleMode.Font
        this.ClientSize <- System.Drawing.Size(832,585)

        this.Controls.Add button2
        this.Controls.Add button1
        this.Controls.Add pictureBox1

        this.Name <- "Form1"
        this.Text <- "Take Snapshot"

        // this.Load += new System.EventHandler(this.Form1_Load_1);
        //this.Load.Add |> ignore

        (pictureBox1 :> System.ComponentModel.ISupportInitialize).EndInit();
        this.ResumeLayout(false);

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
