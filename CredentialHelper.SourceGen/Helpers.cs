using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis.Options;

using static System.Net.Mime.MediaTypeNames;

namespace CredentialHelper.SourceGen;
// putting things in here that may fail to load at assembly time
static internal class Helpers
{

    public static string GetLastCommitHash(string? workingDirectory)
    {
        try
        {
            return TryGetLastCommit(workingDirectory)?.Replace("\"", "'") ?? "ugh";
            //return "";
        } catch (Exception ex)
        {
            return ex.Message + "uh";
        }
    }

    public static string TryGetLastCommit(string? workingDirectory)
    {
        try
        {
            var (ec, outs) = RunWithWhereIfNecessary("git", "log -n 1", workingDirectory);
            Console.WriteLine($"ec:{ec}");
            if (ec != 0)
            {
                return $"ec:{ec}";
            }
            try
            {

                var commitHash =
                    outs
                        .Where(line => !String.IsNullOrWhiteSpace(line))
                        .Select(line => line.Trim().After("commit ").Before(" "))
                        .Where(line => !String.IsNullOrWhiteSpace(line))
                        .FirstOrDefault() ?? ec.ToString();
                return commitHash;
            } catch (Exception ex)
            {
                System.Diagnostics.Debugger.Launch();
                return $"{TryGetTypeName(ex)}:{ex.Message}:{ex.StackTrace.Replace("\"", "'")}";

            }
        } catch (Exception ex)
        {
            System.Diagnostics.Debugger.Launch();
            return $"{TryGetTypeName(ex)}:{ex.Message}:{ex.StackTrace.Replace("\"", "'")}";
        }
    }
    public static (int, List<string>) RunWithWhereIfNecessary(string cmd, string args, string? workingDirectory)
    {
        try
        {
            return ExecuteProcessHarnessed(cmd, args, workingDirectory);

        } catch (Exception ex)
        {
            Console.Error.WriteLine($"Needed where I guess?: {TryGetTypeName(ex)}-{ex.Message}");
            var (ec, text) = Where(cmd);
            //Helpers.printOuts text
            var cmd2 = text[0];
            //printfn "found at '%s'" cmd
            return ExecuteProcessHarnessed(cmd2, args, workingDirectory);
        }
    }
    public static (int, List<string>) Where(string cmd)
    {
        return ExecuteProcessHarnessed("where", cmd, null);
    }

    public static int ExecuteProcessCaptured(string exe, string args, string? workingDirectory, Action<string> listener)
    {

        var psi = new System.Diagnostics.ProcessStartInfo(exe, args) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        if (workingDirectory != null) psi.WorkingDirectory = workingDirectory;

        var p = System.Diagnostics.Process.Start(psi);

        p.OutputDataReceived += (_, args) => listener(args.Data);
        p.ErrorDataReceived += (_, args) => listener(args.Data);
        p.BeginErrorReadLine();
        p.BeginOutputReadLine();
        p.WaitForExit();
        return p.ExitCode;
    }

    public static (int, List<string>) ExecuteProcessHarnessed(string exe, string args, string? workingDirectory)
    {
        var outs = new System.Collections.Concurrent.ConcurrentQueue<string>();
        Action<string> addOut = outs.Enqueue;
        var ec = ExecuteProcessCaptured(exe, args, workingDirectory, addOut);
        return (ec, outs.ToList());
    }

    public static string TryGetTypeName(object obj)
    {
        try
        {
            if (obj != null)
                return obj.GetType().Name;
            return "<null>";

        } catch { return "<unk>"; }
    }

    public static string? Encode(this string? value, string delimiter, string replacement)
    {
        if (string.IsNullOrEmpty(value)) return null;
        return value!.Replace(delimiter, replacement);
    }

    public static string? SurroundIf(this string value, Func<string, bool> predicate, string surroundings)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (predicate(value))
        {
            return $"{surroundings}{value}{surroundings}";
        }
        return value;
    }

    public static string? After(this string? value, string delimiter)
    {
        if (value.tryIndexOf(delimiter) is int i)
        {
            return value!.Substring(i + delimiter.Length);
        } else return null;
    }

    public static string? Before(this string? value, string delimiter)
    {
        if (value.tryIndexOf(delimiter) is int i)
        {
            return value!.Substring(0, i - 1);
        } else return null;
    }

    public static int? tryIndexOf(this string? value, string delimiter)
    {
        if (string.IsNullOrEmpty(delimiter)) throw new InvalidOperationException("Delimiter was null");
        if (string.IsNullOrEmpty(value)) return default;
        var i = value!.IndexOf(delimiter);
        if (i >= 0) return i;
        return default;

    }
}
