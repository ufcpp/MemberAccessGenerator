using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Immutable;
using System.IO;

class Program
{
    static void Main()
    {
        var source = @"using MemberAccess;

[ByIndexAttribute, ByNameAttribute, Enumerate]
partial record Point(int X, int Y);

[ByIndexAttribute]
partial record Point1(int X, int Y);

[ByNameAttribute]
partial record Point2(int X, int Y);

[Enumerate]
partial record Point3(int X, int Y);

namespace Sample
{
    [ByIndexAttribute, ByNameAttribute, Enumerate]
    partial record Point(int X, int Y);

    namespace Sample
    {
        [ByIndexAttribute]
        partial record Point1(int X, int Y);
    }
}

namespace System.Runtime.CompilerServices
{
    internal class IsExternalInit : Attribute { }
}
";

        var compilation = Compile(source);

        foreach (var diag in compilation.GetDiagnostics())
        {
            Console.WriteLine(diag);
        }
    }

    private static Compilation Compile(string source)
    {
        var opt = new CSharpParseOptions(languageVersion: LanguageVersion.Preview, kind: SourceCodeKind.Regular, documentationMode: DocumentationMode.Parse);
        var copt = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

        var dotnetCoreDirectory = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        var compilation = CSharpCompilation.Create("test",
            syntaxTrees: new[] { SyntaxFactory.ParseSyntaxTree(source, opt) },
            references: new[]
            {
                AssemblyMetadata.CreateFromFile(typeof(object).Assembly.Location).GetReference(),
                MetadataReference.CreateFromFile(Path.Combine(dotnetCoreDirectory, "netstandard.dll")),
                MetadataReference.CreateFromFile(Path.Combine(dotnetCoreDirectory, "System.Runtime.dll")),
            },
            options: copt);

        // apply the source generator
        var driver = new CSharpGeneratorDriver(opt, ImmutableArray.Create<ISourceGenerator>(new MemberAccessGenerator.MemberAccessGenerator()), null, ImmutableArray<AdditionalText>.Empty);
        driver.RunFullGeneration(compilation, out var resultCompilation, out _);

        return resultCompilation;
    }
}
