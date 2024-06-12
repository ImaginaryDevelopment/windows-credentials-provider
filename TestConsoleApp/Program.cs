namespace TestConsoleApp
{
    using System;

    class Program
    {
        static void Main()
        {
            var networkCredential = CredentialsDialog.GetCredentials("Hey!", "We would like a login.");

            if (networkCredential != null)
            {
                Console.WriteLine($"Username: \'{networkCredential.UserName}\'");
            }
            else
            {
                Console.WriteLine("No credential detected.");
                using (var form1 = new CredentialHelper.CameraControl.Form1()){
                    form1.ShowDialog();
                }
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
    }
}
