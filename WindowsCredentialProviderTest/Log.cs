﻿namespace WindowsCredentialProviderTest
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;

    using static Reusable;
    using CredentialHelper;

    public static class Log
    {
        public static void LogTextWithCaller(string text, EventLogType elt = null, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerName = "")
        {
            var cfp = Reusable.tryMungeCallerFilePath(callerFilePath);
            // try getting just the file name
            LogText($"{text}: {callerName}:{cfp}");
        }
        public static void LogText(string text, Microsoft.FSharp.Core.FSharpOption<EventLogType> eltOpt)
        {
            if(eltOpt?.Value is { } elt)
            {
                LogText(text, elt);
            } else
            {
                LogText(text);
            }
        }

        public static void LogText(string text, EventLogType elt = null)
        {
            var logFileNames = new[]
            {
                @"C:\net481\CredentialProviderLog.log.txt",
                // we should try to cd to codebase maybe?
                "CredentialProviderLog.log.txt"
            }.toList();
            var elt2 = elt ?? EventLogType.Warning;

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
        public static void LogMethodCall(EventLogType elt = null, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerName = "")
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
        }
    }
}
