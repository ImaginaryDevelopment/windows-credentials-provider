namespace TestConsoleApp
{
    using System;

    using WindowsCredentialProviderTest;

    class Program
    {
        static void Main()
        {
            // arg[0] is our app name, right?
            var runType = CredentialHelper.CommandParser.CommandType.AttemptLogin; // CredentialHelper.CommandParser.getCommandType(Environment.GetCommandLineArgs());
            Console.WriteLine(runType);
            Log.LogText("(" + CredentialHelper.UI.PartialGen.Built.ToString("yyyyMMdd") + "): Running as :" + runType.ToString());
            if (runType.IsAttemptLogin)
            {
                var networkCredential = CredentialsDialog.GetCredentials("Hey!", "We would like a login.");

                if (networkCredential != null)
                {
                    Console.WriteLine($"Username: \'{networkCredential.UserName}\'");
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
                using (var form1 = new CredentialHelper.UI.Form1())
                {
                    if (form1.InvokeRequired)
                    {
                        form1.Invoke(new Action(() => form1.ShowDialog()));
                    } else
                {
                    form1.ShowDialog();
                }
                }

            } else if (runType.IsComInvoke)
            {
                TryComInvoke();


            } else if (runType.IsApiCall)
            {
                TryApiCall();
            } else
            {
                Console.WriteLine("Run Type unimplemented:'{0}'", runType);
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        static void TryComInvoke()
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

        static void TryApiCall()
        {
            Log.LogMethodCall();
            var baseUrl = CredentialHelper.ApiClient.BaseUrl.TryCreate(System.Environment.GetEnvironmentVariable("devapi")).ResultValue;
            var r = CredentialHelper.ApiClient.tryValidate(baseUrl, new CredentialHelper.ApiClient.AuthPost("1")).Result;
            Console.WriteLine(r);
        }

        static TestWindowsCredentialProvider ValidateCP(object o)
        {
            Log.LogMethodCall();

            if (o is WindowsCredentialProviderTest.TestWindowsCredentialProvider twcp)
            {
                Console.WriteLine("found twcp");
                Console.WriteLine(twcp.SayHello());
                return twcp;
            } else if (o is WindowsCredentialProviderTest.ITestWindowsCredentialProvider icp)
            {
                Console.WriteLine("found icp");
            }
            return null;
        }
    }
}
