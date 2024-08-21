using System;
using System.Collections;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace CredentialHelper.SourceGen;

// if there are package complaints in the output window see https://stackoverflow.com/questions/77513481/net-8-build-issue-the-analyzer-assembly-references-version-4-8-0-0-of-the-co might need an sdk change perhaps?
// was resolved or stopped being an error without using a global.json

// for debugging: maybe try https://github.com/JoanComasFdz/dotnet-how-to-debug-source-generator-vs2022
// see also https://andrewlock.net/exploring-dotnet-6-part-9-source-generator-updates-incremental-generators/ for newer source gen info
[Generator]
public class Class1 : ISourceGenerator // IIncrementalGenerator //  
{
    const string Namespace = "CredentialHelper.SourceGenerated";
    //string? _projectDir;

    //[System.Diagnostics.CodeAnalysis.SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1035:Do not use APIs banned for analyzers", Justification = "Recording the directory we are called from")]
    //static System.Collections.Generic.IEnumerable<(string,Func<string>)> GetGenerations()
    //{
    //    var gens = new[] { NowDelimiter,LastCommitHashDelimiter,NamespaceDelimiter , GenDirectoryDelimiter };
    //    foreach(var g in gens)
    //    {
    //        switch (g)
    //        {
    //            case NowDelimiter:
    //                yield return (NowDelimiter,() => DateTime.Now.Ticks.ToString());
    //                break;
    //            case LastCommitHashDelimiter:
    //                yield return (LastCommitHashDelimiter, () => Helpers.GetLastCommitHash());
    //                break;
    //            case NamespaceDelimiter:
    //                yield return (NamespaceDelimiter, () => Namespace);
    //                break;
    //            case GenDirectoryDelimiter:
    //                yield return (GenDirectoryDelimiter, () => Environment.CurrentDirectory.Replace("\\","\\\\"));
    //                break;
    //        }
    //    }

    //}

    [System.Diagnostics.CodeAnalysis.SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1035:Do not use APIs banned for analyzers", Justification = "Recording the directory we are called from")]
    public void GenerateIt(Action<string, SourceText> addSource)
    {
        //var lastCommitHash = Helpers.GetLastCommitHash(_projectDir)?.SurroundIf(x => x.Contains("\""), "\"\"")?.Replace("\r\n", "-");
        var projPath = Environment.GetEnvironmentVariable("qrCodeDir");
        var lastCommitHash = Helpers.GetLastCommitHash(projPath)?.SurroundIf(x => x.Contains("\""), "\"\"")?.Replace("\r\n", "-");
        var now = System.DateTime.Now.Ticks;
        var genDir = Environment.CurrentDirectory;
        //var now2 = new System.DateTime(now);
        var sourceText = $$"""
                namespace {{Namespace}};
                public static class BuildInfo {
                    public static System.DateTime Built => new System.DateTime({{now}});
                    public static string LastCommitHash => "{{lastCommitHash}}";
                    public static string GenDir => @"{{genDir}}";
                    public static string QRCodeDir => @"{{projPath}}";
                }
                """;
        //.Replace("{lastCommitHash}", "\"\"" + lastCommitHash.Replace("\r\n","-")+"\"\"").Replace("{namespace}", Namespace);
        addSource("AssemblyInfo2.cs", SourceText.From(sourceText, Encoding.UTF8));
    }


    //public void Initialize(IncrementalGeneratorInitializationContext context)
    //{

    //    //context.RegisterForPostInitialization
    //    context.RegisterPostInitializationOutput(ctx =>
    //    {
    //        GenerateIt(ctx.AddSource);
    //    });
    //}


    void ISourceGenerator.Execute(GeneratorExecutionContext context)
    {
        //_projectDir = context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.projectdir", out var result) ? result : null;
        //var keys = context.AnalyzerConfigOptions.GlobalOptions.Keys.ToList();
        //System.Diagnostics.Debugger.Launch();
        //keys.ForEach(x => Console.WriteLine(x) );
        //GenerateIt(context.AddSource);
    }

    void ISourceGenerator.Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForPostInitialization(ctx =>
        {
            GenerateIt(ctx.AddSource);
        });

    }
}
