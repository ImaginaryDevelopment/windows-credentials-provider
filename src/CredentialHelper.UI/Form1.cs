using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using CredentialHelper;

namespace CredentialHelper.UI
{
    public partial class Form1 : Form, IDisposable
    {

        readonly CredentialHelper.QRCode.QrManager qrControl = new QRCode.QrManager();
        // TODO: rename once translation is complete
        readonly CredentialHelper.CameraControl.CameraControl imControl;

        readonly CredentialHelper.CameraControl.ProtectedValue<int> cameraIndex;

        readonly Action onCameraIndexChange;

        readonly List<(string, IDisposable)> disposables = new List<(string, IDisposable)>();

        public ApiClient.VerificationResult? VerificationResult { get; set; }

        public Form1()
        {
            InitializeComponent();
            imControl = CameraControl.UI.createCameraControl(this.pictureBox1);
            Func<bool> pvDelegate = () => CredentialHelper.CameraControl.CameraState.Stopped.Equals(imControl.CameraState.Value)
                && !imControl.IsRunning;
            //switch (imControl.CameraState.Value)
            //{
            //    case CredentialHelper.CameraControl.CameraState.Stopped:
            //        break;
            //}

            //var items = new[] { 1, 2, 3, 4 };
            //items.toList();
            //var pvDelegate2 = pvDelegate.toFSharpFunc<bool>();

            cameraIndex = CredentialHelper.CameraControl.ProtectedValue<int>.CCreate(0, pvDelegate);
            onCameraIndexChange = CHelpers.createLatchedFunctionA(() => CameraControl.UI.onCameraIndexChangeRequest(cameraIndexComboBox, cameraIndex, imControl, pictureBox1));

            disposables.Add(("qrControl", imControl));
            disposables.Add(("combobox camerastate", CameraControl.UI.hookUpCameraStateChanges(imControl, runButton, snapButton, cameraIndexComboBox)));
            // there is a startup time to grabbing the camera and starting to display it on the screen

            // relies on capture camera invoking the setter above to kick off post-initializing work
            imControl.CaptureCamera(cameraIndex.Value);
        }

        public void CleanPictureBox()
        {
            CredentialHelper.CameraControl.UI.cleanPictureBox(this.pictureBox1);
        }

        void runButton_Click(object sender, EventArgs e)
        {
            CredentialHelper.CameraControl.UI.onRunRequest(imControl, this.cameraIndex, this.pictureBox1);
            CameraControl.UI.setRunText(imControl.CameraState.Value, this.runButton);
        }

        void cameraIndexComboBox_SelectedValueChanged(object sender, EventArgs e)
        {
            onCameraIndexChange();
        }

        async void snapButton_Click(object sender, EventArgs e)
        {
            void ShowMsgBox(string msg)
                => this.SmartInvoke(_ => MessageBox.Show(msg));

            try
            {
                //this.pictureBox2.Image = this.pictureBox1.Image;
                // trying to stop the ui from freezing while it processes
                var result = await Task.Run(() => CameraControl.UI.onSnapRequest(imControl, qrControl));
                if (result.TryGetQRValidated()?.Value is { } qrResult)
                {
                    if (qrResult.IsOk && qrResult.ResultValue is { } value)
                    {
                        this.VerificationResult = value;
                        //ShowMsgBox("Success");
                        this.Close();
                    } else
                    {
                        ShowMsgBox(qrResult.ErrorValue);
                    }
                } else if (result.IsNoQrCodeFound)
                {
                    ShowMsgBox("No QR Code found");
                } else if (result.IsInvalidCameraState)
                {
                    this.SmartInvoke(_ => MessageBox.Show("Camera state invalid"));
                } else if (result.TryGetError() is { } eMsg && eMsg?.Length > 0)
                {
                    ShowMsgBox(eMsg);
                } else
                {
                    ShowMsgBox("How did we get here?");
                }
            } catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
            }
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
                        Console.Write($"disposing: '{x.Item1}'");
                        x.Item2.Dispose();
                    } catch { }
                });
            } catch { }
            base.Dispose(disposing);
        }
    }
}
