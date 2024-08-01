using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using CredentialHelper;

namespace CredentialHelper.UI;


public partial class Form1 : Form, IDisposable
{

    readonly CredentialHelper.QRCode.QrManager qrManager = new();
    readonly bool ctsIsOwn;
    readonly CancellationTokenSource cts;
    // TODO: rename once translation is complete
    readonly CredentialHelper.CameraControl.CameraControl cameraControl;

    readonly CredentialHelper.CameraControl.ProtectedValue<int> cameraIndex;

    readonly Action onCameraIndexChange;

    readonly CredentialHelper.CompositionRoot.Worker worker;

    readonly List<(string, IDisposable)> disposables = new();

    public static AppSettings.AppConfig AppConfig => AppSettings.getConfiguration(Reusable.Cereal.deserializer, Reporter.Instance, null);

    public ApiClient.VerificationResult? VerificationResult { get; set; }

    public Form1(CancellationTokenSource cts = null)
    {
        InitializeComponent();

        if (cts == null)
        {
            ctsIsOwn = true;
            cts = new CancellationTokenSource();
        } else
        {
            ctsIsOwn = false;
        }
        this.cts = cts ?? throw new InvalidOperationException("cts must be set");

        cameraControl = CameraControl.UI.createCameraControl(this.pictureBox1, this.cts.Token);
        Func<bool> pvDelegate = () => CredentialHelper.CameraControl.CameraState.Stopped.Equals(cameraControl.CameraState.Value)
            && !cameraControl.IsRunning;
        //switch (imControl.CameraState.Value)
        //{
        //    case CredentialHelper.CameraControl.CameraState.Stopped:
        //        break;
        //}

        //var items = new[] { 1, 2, 3, 4 };
        //items.toList();
        //var pvDelegate2 = pvDelegate.toFSharpFunc<bool>();

        cameraIndex = CredentialHelper.CameraControl.ProtectedValue<int>.CCreate(0, pvDelegate);
        onCameraIndexChange = CHelpers.createLatchedFunctionA(() => CameraControl.UI.onCameraIndexChangeRequest(cameraIndexComboBox, cameraIndex, cameraControl, pictureBox1, this.cts.Token));

        disposables.Add(("qrControl", cameraControl));
        disposables.Add(("combobox camerastate", CameraControl.UI.hookUpCameraStateChanges(cameraControl, runButton, snapButton, cameraIndexComboBox)));
        disposables.Add(("cts", this.cts));
        // there is a startup time to grabbing the camera and starting to display it on the screen

        // relies on capture camera invoking the setter above to kick off post-initializing work
        cameraControl.CaptureCamera(cameraIndex.Value, this.cts.Token);
        this.Text = this.Text + "(" + PartialGen.Built.ToString() + ")";
#if DEBUG
        this.btnDiag.Click += this.BtnDiag_Click;
        this.Text += "-DEBUG";
#else
        this.btnDiag.Visible = false;
        this.btnDiag.Enabled = false;
    
#endif

        worker = new(() => new CompositionRoot.SystemState( this.IsHandleCreated, cameraControl.IsRunning), cameraControl, qrManager, AppConfig, v => OnVerified(v), this.cts);

    }

    public void RequestCancellation() => InitiateCancel();

#if DEBUG
    void BtnDiag_Click(object sender, EventArgs e)
    {
        this.btnDiag.Enabled = false;
        System.Diagnostics.Process.Start("taskmgr");

    }

#endif

    public void CleanPictureBox()
    {
        //CredentialHelper.CameraControl.UI.cleanPictureBox(null,this.pictureBox1);
    }

    void runButton_Click(object sender, EventArgs e)
    {
        CredentialHelper.CameraControl.UI.onRunRequest(cameraControl, this.cameraIndex, this.pictureBox1, this.cts.Token);
        CameraControl.UI.setRunText(cameraControl.CameraState.Value, this.runButton);
    }

    void cameraIndexComboBox_SelectedValueChanged(object sender, EventArgs e)
    {
        onCameraIndexChange();
    }

    void OnVerified(ApiClient.VerificationResult creds)
    {
        this.VerificationResult = creds;
        InitiateCancel();
        this.SmartInvoke(_ => this.Close());
        //ShowMsgBox("Success", _ => this.Close());
        this.Close();
    }

    async Task VerifyQrCode(string qrCode)
    {
        var verifyResult = await Task.Run(() => CameraControl.UI.verifyQrCode(AppConfig, qrCode, this.cts.Token), this.cts.Token);
        if (verifyResult.ResultValue is { } creds)
        {
            Console.WriteLine($"Found credentials from qr code: {qrCode}");
            OnVerified(creds);
        } else
        {
            if (verifyResult.ErrorValue is { } vError)
            {
                ShowMsgBox(vError.ToString());

            } else ShowMsgBox("Qr Code failed verification");
        }
    }

    void ShowMsgBox(string msg, Action<DialogResult>? addlSmartInvokingAfter = null)
        => this.SmartInvoke(_ =>
        {
            var mbResult = MessageBox.Show(msg);
            addlSmartInvokingAfter?.Invoke(mbResult);
        });

    async void snapButton_Click(object sender, EventArgs e)
    {

        if (worker.IsRunning)
        {
            worker.Pause = true;
            return;
        }

        try
        {
            //this.pictureBox2.Image = this.pictureBox1.Image;
            // trying to stop the ui from freezing while it processes
            var rBm = await Task.Run(() => CameraControl.UI.onSnapRequest(cameraControl));
            if (rBm.IsOk && rBm.tryGetValue()?.Value is { } dispBm && dispBm.TryGet("SnapButton_click")?.Value is { } bm)
            {
                var r2 = qrManager.TryDecode(bm, cts.Token)?.TryGetCode();
                if (r2 != null && r2?.Value is { } qrValue && qrValue.IsValueString())
                {
                    txtQrValue.Text = qrValue;
                    await VerifyQrCode(qrValue);
                } else
                {
                    // no qr code found in image, ignore
                    return;
                }
            }
        } catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
        }
    }

    async void btnLogin_Click(object sender, EventArgs e)
    {
        if (this.txtQrValue.Text.IsValueString())
        {
            await VerifyQrCode(this.txtQrValue.Text);
        } else
        {
            ShowMsgBox("No QrValue found");
        }
    }

    void InitiateCancel()
    {
        if (!this.cts.IsCancellationRequested)
        {
            this.cts.Cancel();
        }
    }

    //protected override void OnShown(EventArgs e)
    //{
    //    worker?.Start();
    //    base.OnShown(e);
    //}

    protected override void OnClosing(CancelEventArgs e)
    {
        InitiateCancel();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        InitiateCancel();
        base.OnClosed(e);
    }

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        try
        {
            disposables.ForEach(x =>
            {
                try
                {
                    Console.WriteLine($"disposing: '{x.Item1}'");
                    x.Item2.Dispose();
                } catch { }
            });
        } catch { }
        base.Dispose(disposing);
    }
}
public class Reporter : Reusable.Reporter
{
    static Reporter instance = new();
    public static Reporter Instance => instance;
    private Reporter() { }

    public void Log(string value) => Console.WriteLine(value);
    public void LogError(string value1, Exception value2) => Console.Error.WriteLine($"{value1}:{value2.GetType().Name}:{value2.Message}");
    public void LogError(string value) => Console.Error.WriteLine(value);
    public void Log<T>(string text, T value)
    {
        try
        {
            var v = Reusable.Cereal.serialize(value);
            Console.WriteLine($"{text}:{v}");
        } catch
        {
            Console.WriteLine($"{text}:serError");
        }
    }
}
