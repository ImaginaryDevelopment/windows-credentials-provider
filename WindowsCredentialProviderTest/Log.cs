namespace WindowsCredentialProviderTest
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;

    using CredentialHelper;

    public static class Log
    {
        public static void LogText(string text, Logging.EventLogType elt = null)
        {
            var logFileNames = new[]
            {
                @"C:\net481\CredentialProviderLog.log.txt",
                "CredentialProviderLog.log.txt"
            }.toList();
            var elt2 = elt ?? Logging.EventLogType.Warning;

            var fla = new CredentialHelper.Logging.FullLoggingArgs("WindowsCredentialProviderTest", CredentialHelper.Logging.LogListAttemptType.TryAll, logFileNames);
            CredentialHelper.Logging.tryLoggingsWithFallback(fla, text, elt2);
            try
            {
                Console.WriteLine(text);
            } catch (Exception)
            {
                try
                {
                    Console.WriteLine("(Skipped logging)");
                } catch (Exception)
                {
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void LogMethodCall([CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerName = "")
        {
            if (callerName.IsNullOrEmpty())
            {
                // fallback on old method of auto-logging the caller name
                var st = new StackTrace();
                var sf = st.GetFrame(1);

                var methodBase = sf.GetMethod();
                callerName = methodBase.DeclaringType?.Name + "::" + methodBase.Name;
            } else callerName = callerFilePath.IsNullOrEmpty() ? callerName : callerFilePath + ":" + callerName;

            LogText(callerName);
        }
    }
}
