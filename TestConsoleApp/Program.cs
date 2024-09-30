namespace TestConsoleApp;

using System;
using System.Threading;

using CredentialHelper;
using CredentialHelper.UI;

using WindowsCredentialProviderTest;


class Program
{
    static void Main()
    {
        // arg[0] is our app name, right?
        var runType =
#if DEBUG
            CredentialHelper.CommandParser.CommandType.NewApiCall("320016909");
#else
            CredentialHelper.CommandParser.getCommandType(Environment.GetCommandLineArgs());
#endif

        Console.WriteLine(runType);
        Log.LogText("(" + CredentialHelper.Generated.PartialGen.Built.ToString("yyyyMMdd") + "): Run type:" + runType.ToString());
        if (runType.IsAttemptLogin)
        {
            var networkCredential = CredentialsDialog.GetCredentials("Hey!", "We would like a login.");

            if (networkCredential != null)
            {
                Console.WriteLine($"Username: \'{networkCredential.UserName}\'");
                if (!networkCredential.Domain.IsValueString() && networkCredential.UserName?.Contains("\\") == true)
                {
                    var domain = networkCredential.UserName.Before("\\");
                    var un = networkCredential.UserName.After("\\");

                    networkCredential = new(un, networkCredential.Password, domain: domain);
                }
                RunImpersonation(new CredentialHelper.ApiClient.VerificationResult(networkCredential.UserName, networkCredential.Password, networkCredential.Domain.IsValueString() ? networkCredential.Domain : null));
            } else
            {
                Console.WriteLine("No credential detected.");
                //using (var form1 = new CredentialHelper.CameraControl.Form1())
                //{
                //    form1.ShowDialog();
                //}
            }

        } else if (runType.IsShowUI)
        {
            CredentialHelper.ApiClient.VerificationResult? vr = null;
            using (var form1 = new CredentialHelper.UI.Form1((txt, ll) => Log.LogText(txt, ll)))
            {
                if (form1.InvokeRequired)
                {
                    form1.Invoke(new Action(() => form1.ShowDialog()));
                } else
                {
                    form1.ShowDialog();
                }
                vr = form1.VerificationResult;
            }
            if (vr != null)
            {
                RunImpersonation(vr);
            }

        } else if (runType.IsComInvoke)
        {
            TryComInvoke();

        } else if (runType.IsApiCall)
        {
            if(CredentialHelper.CommandParser.CommandType.TryGetApiQrCode(runType)?.Value is string qrCode && !String.IsNullOrWhiteSpace(qrCode))
            {
                TryApiCall(qrCode);
            } else TryApiCall();

        } else if (runType.IsShowArgs)
        {
            ShowArgs();
        } else if (runType.IsOutputDiagnostics)
        {
            CredentialHelper.CompositionRoot.outputDiagnostics(Guid.Parse(Constants.CredentialProviderUID));
        } else
        {
            Console.WriteLine("Run Type unimplemented:'{0}'", runType);
        }

        Console.WriteLine("Press any key to continue...");
        Console.ReadKey();
    }

    static void RunImpersonation(CredentialHelper.ApiClient.VerificationResult vr)
    {
        Log.LogText("Attempting impersonation");
        var impSuccess = ImpersonationHelper.RunImpersonated(vr, () => Log.LogText("Impersonation running", BReusable.EventLogType.Warning));
        if (!impSuccess)
        {
            Console.Error.WriteLine($"Failed to impersonate for:{vr.Domain}\\{vr.Username}");
        }
    }

    static void TryComTestCredProviderInvoke()
    {
        var g = System.Guid.Parse(WindowsCredentialProviderTest.Constants.CredentialProviderUID);
        var tnr = new CredentialHelper.ComHelper.TypeNameReference(nameof(WindowsCredentialProviderTest), nameof(WindowsCredentialProviderTest.TestWindowsCredentialProvider));
        var t = typeof(WindowsCredentialProviderTest.TestWindowsCredentialProvider);
        var progId = WindowsCredentialProviderTest.Constants.CredentialProviderProgId;

        foreach (var (name, r) in CredentialHelper.ComHelper.tryAllCom<TestWindowsCredentialProvider>(g, tnr, t, progId, ValidateCP))
        {
            if (r.IsOk && r.ResultValue != null)
            {
                Console.WriteLine(name + ": ok - " + r.ResultValue);
                return;
            } else if (r.IsOk && r.ResultValue == null)
            {
                Console.Error.WriteLine(name + ": ok - null");
            } else if (r.IsError && r.ErrorValue != null)
            {
                Console.Error.WriteLine(name + ": error - " + r.ErrorValue);
            } else if (r.IsError && r.ErrorValue == null)
            {
                Console.Error.WriteLine(name + ": error - null");
            } else
            {
                Console.Error.WriteLine(name + ": unknown - " + r);
            }
        }
    }
    static void TryComFilterInvoke()
    {
    }

    static void TryComInvoke()
    {
        TryComTestCredProviderInvoke();
    }

    static void ShowArgs()
    {
        CredentialHelper.CommandParser.showHelp();
    }

    static void TryApiCall(string qrCode = null)
    {
        Log.LogMethodCall();
        var devApiUrl = Form1.AppConfig.DevApi;
        //var baseUrl = CredentialHelper.ApiClient.BaseUrl.TryCreate(devApiUrl).ResultValue;
        //var qrCodeValue = qrCode?.ToString() ?? "1";
        //var r = CredentialHelper.ApiClient.tryValidate(Form1.AppConfig, baseUrl, new CredentialHelper.ApiClient.AuthPost(qrCodeValue), ct: CancellationToken.None).Result;
        CompositionRoot.tryApiCall(Form1.AppConfig, devApiUrl, qrCode);
    }

    static TestWindowsCredentialProvider ValidateCP(object o)
    {
        Log.LogMethodCall();

        if (o is WindowsCredentialProviderTest.TestWindowsCredentialProvider twcp)
        {
            Console.WriteLine("found twcp");
            Console.WriteLine(twcp.SayHello());
            return twcp;
        } else if (o is WindowsCredentialProviderTest.ITestWindowsCredentialProvider _icp)
        {
            Console.WriteLine("found icp");
        }
        return default;
    }
}
