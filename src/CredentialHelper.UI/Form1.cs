﻿using System;
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
            this.Text = this.Text + "(" + CredentialHelper.UI.BuildInfo.Built.ToString() + ")";

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

        void OnVerified(ApiClient.VerificationResult creds)
        {
            this.VerificationResult = creds;
            ShowMsgBox("Success", _ => this.Close());
            this.Close();
        }

        async Task VerifyQrCode(string qrCode)
        {
            var verifyResult = await Task.Run(() => CameraControl.UI.verifyQrCode(qrCode));
            if (verifyResult.ResultValue is { } creds)
            {
                OnVerified(creds);
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
            try
            {
                //this.pictureBox2.Image = this.pictureBox1.Image;
                // trying to stop the ui from freezing while it processes
                var rBm = await Task.Run(() => CameraControl.UI.onSnapRequest(imControl));
                if (rBm.IsOk && rBm.tryGetValue()?.Value is { } bm)
                {
                    var r2 = qrControl.TryDecode(bm);
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
