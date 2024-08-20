namespace WindowsCredentialProviderTest
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;

    using static Reusable;
    using CredentialHelper;
    using System.Collections.Generic;

    public static class Log
    {
        public static void LogTextWithCaller(string text, EventLogType elt = null, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerName = "")
        {
            try
            {

                var cfp = Reusable.tryMungeCallerFilePath(callerFilePath);
                // try getting just the file name
                LogText($"{text}: {callerName}:{cfp}");
            } catch
            {
                try
                {
                    LogText("LogTextWithCaller failed");
                    LogText($"{text}: {callerName}:{callerFilePath}");
                } catch { }
            }

        }
        public static void LogText(string text, Microsoft.FSharp.Core.FSharpOption<EventLogType> eltOpt)
        {
            if (eltOpt?.Value is { } elt)
            {
                LogText(text, elt);
            } else
            {
                LogText(text);
            }
        }

        public static void LogText(string text, EventLogType elt = null)
        {
            var elt2 = elt ?? EventLogType.Warning;
            try
            {

                var logFileNames = new[]
                {
                @"C:\net481\CredentialProviderLog.log.txt",
                // we should try to cd to codebase maybe?
                "CredentialProviderLog.log.txt"
            }.toList();

                var fla = new CredentialHelper.Logging.FullLoggingArgs("WindowsCredentialProviderTest", CredentialHelper.Logging.LogListAttemptType.TryAll, logFileNames);
                CredentialHelper.Logging.tryLoggingsWithFallback(fla, text, elt2);
            } catch
            {
                try
                {

                    var fla = new CredentialHelper.Logging.FullLoggingArgs("WindowsCredentialProviderTest", CredentialHelper.Logging.LogListAttemptType.TryAll, Microsoft.FSharp.Collections.FSharpList<string>.Empty);
                    CredentialHelper.Logging.tryLoggingsWithFallback(fla, text, elt2);
                } catch
                {

                }
            }
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
        public static void LogMethodCall(EventLogType elt = null, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerName = "")
        {
            try
            {

                if (callerName.IsNullOrEmpty())
                {
                    // fallback on old method of auto-logging the caller name
                    var st = new StackTrace();
                    var sf = st.GetFrame(1);

                    var methodBase = sf.GetMethod();
                    callerName = methodBase.DeclaringType?.Name + "::" + methodBase.Name;
                } else callerName = callerFilePath.IsNullOrEmpty() ? callerName : callerFilePath + ":" + callerName;

                LogText(callerName, elt ?? EventLogType.Information);
            } catch { }
        }
    }
}
