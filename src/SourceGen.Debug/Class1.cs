using System;
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
    string[] TimestampFile = new string[]
                        {@"using System;",
                 @"// The namespace can be overidden by the /N option:",
                 @"// GenerateTimeStampFile file.cs /N:MyNameSpace",
                 @"// Such settings will override your value here.",
                 @"namespace TimeStamp",
                 @"    {",
                 @"    /// <summary>",
                 @"    /// Static Timestamp related data.",
                 @"    /// </summary>",
                 @"    /// <remarks>",
                 @"    /// THIS FILE IS CHANGED BY EXTERNAL PROGRAMS.",
                 @"    /// Do not modify the namespace, as it may be overwritten. You can",
                 @"    ///    set the namespace with the /N option.",
                 @"    /// Do not modify the definition of BuildAt as your changes will be discarded.",
                 @"    /// Do not modify the definition of TimeStampedBy as your changes will be discarded.",
                 @"    /// </remarks>",
                 @"    public static class Timestamp",
                 @"        {",
                 @"        /// <summary>",
                 @"        /// The time stamp",
                 @"        /// </summary>",
                 @"        /// <remarks>",
                 @"        /// Do not modify the definition of BuildAt as your changes will be discarded.",
                 @"        /// </remarks>",
                 @"        public static DateTime BuildAt { get { return new DateTime(???); } } //--**",
                 @"        /// <summary>",
                 @"        /// The program that time stamped it.",
                 @"        /// </summary>",
                 @"        /// <remarks>",
                 @"        /// Do not modify the definition of TimeStampedBy as your changes will be discarded.",
                 @"        /// </remarks>",
                 @"        public static string TimeStampedBy { get { return @""???""; } } //--**",
                 @"        }",
                 @"    }" };
    public void GenerateIt(Action<string, SourceText> addSource)
    {
        var lastCommitHash = Helpers.GetLastCommitHash();
        var now = System.DateTime.Now.Ticks;
        //var now2 = new System.DateTime(now);
        var sourceText = $$"""
                namespace {namespace};
                public static class BuildInfo {
                    public static System.DateTime Built => new System.DateTime({now});
                    public static string LastCommitHash => "{lastCommitHash}";
                }
                """.Replace("{now}", now.ToString()).Replace("{lastCommitHash}", "\"\"" + lastCommitHash.Replace("\r\n", "-") + "\"\"").Replace("{namespace}", Namespace);
        addSource("AssemblyInfo2.cs", SourceText.From(sourceText, Encoding.UTF8));
    }


    public void Initialize(IncrementalGeneratorInitializationContext context)
    {

        //context.RegisterForPostInitialization
        context.RegisterPostInitializationOutput(ctx =>
        {
            GenerateIt(ctx.AddSource);
        });
    }

    void ISourceGenerator.Execute(GeneratorExecutionContext context) { }
    void ISourceGenerator.Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForPostInitialization(ctx =>
        {
            GenerateIt(ctx.AddSource);
        });

    }
}
