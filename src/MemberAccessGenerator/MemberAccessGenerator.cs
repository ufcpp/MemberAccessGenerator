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
    sealed class ByIndexAttribute : Attribute { }

    [System.Diagnostics.Conditional(""COMPILE_TIME_ONLY"")]
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    sealed class ByNameAttribute : Attribute { }

    [System.Diagnostics.Conditional(""COMPILE_TIME_ONLY"")]
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    sealed class EnumerateAttribute : Attribute { }
}
";

        [Flags]
        private enum Flag
        {
            None = 0,
            ByIndex = 1,
            ByName = 2,
            Enumerate = 4,
            All = ByIndex | ByName | Enumerate,
        }

        public void Execute(SourceGeneratorContext context)
        {
            context.AddSource("MemberAccessAttributes", SourceText.From(attributeText, Encoding.UTF8));

            if (!(context.SyntaxReceiver is SyntaxReceiver receiver)) return;

            CSharpParseOptions options = (CSharpParseOptions)((CSharpCompilation)context.Compilation).SyntaxTrees[0].Options;

            Compilation compilation = context.Compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(SourceText.From(attributeText, Encoding.UTF8), options));

            if (!(compilation.GetTypeByMetadataName("MemberAccess.ByIndexAttribute") is { } indexAttributeSymbol)) return;
            if (!(compilation.GetTypeByMetadataName("MemberAccess.ByNameAttribute") is { } nameAttributeSymbol)) return;
            if (!(compilation.GetTypeByMetadataName("MemberAccess.EnumerateAttribute") is { } enumerateAttributeSymbol)) return;

            var buffer = new StringBuilder();

            foreach (var r in receiver.CandidateMethods)
            {
                SemanticModel model = compilation.GetSemanticModel(r.SyntaxTree);

                var flag = getMemberAccessAttribute(model, r);
                if (flag == Flag.None) continue;
                if (r.ParameterList is not { } list) continue;
                if (model.GetDeclaredSymbol(r) is not { } s) continue;

                var generatedSource = generate(s, list, flag);
                var filename = getFilename(s);
                context.AddSource(filename, SourceText.From(generatedSource, Encoding.UTF8));
            }

            string getFilename(INamedTypeSymbol type)
            {
                buffer.Clear();

                foreach (var part in type.ContainingNamespace.ToDisplayParts())
                {
                    if (part.Symbol is { Name: var name } && !string.IsNullOrEmpty(name))
                    {
                        buffer.Append(name);
                        buffer.Append('_');
                    }
                }
                buffer.Append(type.Name);
                buffer.Append("_memberaccess.cs");

                return buffer.ToString();
            }

            string generate(INamedTypeSymbol type, ParameterListSyntax list, Flag flag)
            {
                buffer.Clear();

                if (!string.IsNullOrEmpty(type.ContainingNamespace.Name))
                {
                    buffer.Append(@"namespace ");
                    buffer.Append(type.ContainingNamespace.ToDisplayString());
                    buffer.Append(@" {
");
                }
                buffer.Append("partial record ");
                buffer.Append(type.Name);
                buffer.Append(@"
{");

                if ((flag & Flag.ByIndex) != 0)
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

                if ((flag & Flag.ByName) != 0)
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

                if ((flag & Flag.Enumerate) != 0)
                {
                    buffer.Append(@"
    public System.Collections.Generic.IEnumerable<(string name, object value)> EnumerateMembers()
    {
");
                    foreach (var p in list.Parameters)
                    {
                        buffer.Append("        yield return (nameof(");
                        buffer.Append(p.Identifier.Text);
                        buffer.Append("), (object)");
                        buffer.Append(p.Identifier.Text);
                        buffer.Append(@");
");
                    }

                    buffer.Append(@"    }
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

            Flag getMemberAccessAttribute(SemanticModel model, RecordDeclarationSyntax m)
            {
                if (!(model.GetDeclaredSymbol(m) is { } s)) return default;

                Flag flag = default;
                foreach (var a in s.GetAttributes())
                {
                    if (SymbolEqualityComparer.Default.Equals(a.AttributeClass, indexAttributeSymbol)) flag |= Flag.ByIndex;
                    if (SymbolEqualityComparer.Default.Equals(a.AttributeClass, nameAttributeSymbol)) flag |= Flag.ByName;
                    if (SymbolEqualityComparer.Default.Equals(a.AttributeClass, enumerateAttributeSymbol)) flag |= Flag.Enumerate;
                    if (flag == Flag.All) break;
                }

                return flag;
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
