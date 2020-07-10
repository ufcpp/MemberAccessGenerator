using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Text;

namespace MemberAccessGenerator
{
    [Generator]
    public class MemberAccessGenerator : ISourceGenerator
    {
        private const string attributeText = @"using System;
namespace MemberAccess
{
    [System.Diagnostics.Conditional(""COMPILE_TIME_ONLY"")]
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    sealed class ByIndexAttribute : Attribute
    {
        public ByIndexAttribute() { }
    }

    [System.Diagnostics.Conditional(""COMPILE_TIME_ONLY"")]
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    sealed class ByNameAttribute : Attribute
    {
        public ByNameAttribute() { }
    }
}
";
        public void Execute(SourceGeneratorContext context)
        {
            context.AddSource("MemberAccessAttributes", SourceText.From(attributeText, Encoding.UTF8));

            if (!(context.SyntaxReceiver is SyntaxReceiver receiver)) return;

            CSharpParseOptions options = (CSharpParseOptions)((CSharpCompilation)context.Compilation).SyntaxTrees[0].Options;

            Compilation compilation = context.Compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(SourceText.From(attributeText, Encoding.UTF8), options));

            if (!(compilation.GetTypeByMetadataName("MemberAccess.ByIndexAttribute") is { } indexAttributeSymbol)) return;
            if (!(compilation.GetTypeByMetadataName("MemberAccess.ByNameAttribute") is { } nameAttributeSymbol)) return;

            var buffer = new StringBuilder();

            foreach (var r in receiver.CandidateMethods)
            {
                SemanticModel model = compilation.GetSemanticModel(r.SyntaxTree);

                var (generatesIndex, generatesName) = getMemberAccessAttribute(model, r);
                if (!generatesIndex && !generatesName) continue;
                if (r.ParameterList is not { } list) continue;
                if (model.GetDeclaredSymbol(r) is not { } s) continue;

                var generatedSource = generate(s, list, generatesIndex, generatesName);

                var filename = $"{s.Name}_memberaccess.cs";
                if (!string.IsNullOrEmpty(s.ContainingNamespace.Name))
                {
                    filename = s.ContainingNamespace.Name.Replace('.', '/') + filename;
                }
                context.AddSource(filename, SourceText.From(generatedSource, Encoding.UTF8));
            }

            string generate(INamedTypeSymbol type, ParameterListSyntax list, bool generatesIndex, bool generatesName)
            {
                buffer.Clear();

                if (!string.IsNullOrEmpty(type.ContainingNamespace.Name))
                {
                    buffer.Append(@"namespace ");
                    buffer.Append(type.ContainingNamespace.Name);
                    buffer.Append(@" {
");
                }
                buffer.Append("partial record ");
                buffer.Append(type.Name);
                buffer.Append(@"
{");

                if (generatesIndex)
                {
                    buffer.Append(@"
    public object GetMember(int index) => index switch
    {
");
                    var parameters = list.Parameters;
                    for (int i = 0; i < parameters.Count; i++)
                    {
                        var p = parameters[i];
                        buffer.Append("        ");
                        buffer.Append(i);
                        buffer.Append(" => ");
                        buffer.Append(p.Identifier.Text);
                        buffer.Append(@",
");
                    }

                    buffer.Append(@"        _ => throw new System.Runtime.CompilerServices.SwitchExpressionException(),
    };
");
                }

                if (generatesName)
                {
                    buffer.Append(@"
    public object GetMember(string name) => name switch
    {
");
                    foreach (var p in list.Parameters)
                    {
                        buffer.Append("        nameof(");
                        buffer.Append(p.Identifier.Text);
                        buffer.Append(") => ");
                        buffer.Append(p.Identifier.Text);
                        buffer.Append(@",
");
                    }

                    buffer.Append(@"        _ => throw new System.Runtime.CompilerServices.SwitchExpressionException(),
    };
");
                }

                buffer.Append(@"}
");
                if (!string.IsNullOrEmpty(type.ContainingNamespace.Name))
                {
                    buffer.Append(@"}
");
                }

                return buffer.ToString();
            }

            (bool generatesIndex, bool generatesName) getMemberAccessAttribute(SemanticModel model, RecordDeclarationSyntax m)
            {
                if (!(model.GetDeclaredSymbol(m) is { } s)) return default;

                var i = false;
                var n = false;
                foreach (var a in s.GetAttributes())
                {
                    if (SymbolEqualityComparer.Default.Equals(a.AttributeClass, indexAttributeSymbol)) i = true;
                    if (SymbolEqualityComparer.Default.Equals(a.AttributeClass, nameAttributeSymbol)) n = true;
                    if (i && n) break;
                }

                return (i, n);
            }
        }

        public void Initialize(InitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        class SyntaxReceiver : ISyntaxReceiver
        {
            public List<RecordDeclarationSyntax> CandidateMethods { get; } = new List<RecordDeclarationSyntax>();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                // any field with at least one attribute is a candidate for property generation
                if (syntaxNode is RecordDeclarationSyntax recordDeclarationSyntax
                    && recordDeclarationSyntax.AttributeLists.Count > 0)
                {
                    CandidateMethods.Add(recordDeclarationSyntax);
                }
            }
        }
    }
}
