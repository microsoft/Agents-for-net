// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Agents.Core.Analyzers
{
    /// <summary>
    /// Generates partial class companions for types decorated with attributes that derive from
    /// <c>AgentExtensionAttribute&lt;TExtension&gt;</c>. Each matching attribute produces an
    /// eagerly-initialized property of type <c>TExtension</c> named after the extension type
    /// (e.g. <c>TeamsAgentExtension</c> → <c>TeamsExtension</c>), assigned during construction
    /// via the generated <c>ConfigureExtensions</c> override.
    /// </summary>
    [Generator]
    public class AgentExtensionSourceGenerator : IIncrementalGenerator
    {
        internal const string AgentExtensionAttributeMetadataName =
            "Microsoft.Agents.Builder.App.AgentExtensionAttribute`1";

        private static readonly DiagnosticDescriptor MustBePartialDescriptor = new(
            id: "MAA001",
            title: "Class with AgentExtension attribute must be partial",
            messageFormat: "'{0}' must be declared as 'partial' to use an AgentExtension attribute",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "The source generator for AgentExtension attributes requires the decorated class to be declared with the 'partial' modifier.");

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Resolve the open generic base attribute type once per compilation.
            var baseAttrTypeProvider = context.CompilationProvider.Select(
                static (compilation, _) =>
                    compilation.GetTypeByMetadataName(AgentExtensionAttributeMetadataName));

            // Find all class declarations that have at least one attribute list.
            var classDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) =>
                        node is ClassDeclarationSyntax cls && cls.AttributeLists.Count > 0,
                    transform: static (ctx, ct) =>
                    {
                        var classDecl = (ClassDeclarationSyntax)ctx.Node;
                        var classSymbol = ctx.SemanticModel.GetDeclaredSymbol(classDecl, ct) as INamedTypeSymbol;
                        return (classDecl, classSymbol);
                    })
                .Where(static x => x.classSymbol != null);

            // Collect all candidates before combining so that a partial class split across
            // multiple files (each declaration part yielding a separate syntax node) is
            // deduplicated by symbol and only generates one output file.
            var combined = classDeclarations.Collect().Combine(baseAttrTypeProvider);

            context.RegisterSourceOutput(combined, static (spc, item) =>
            {
                var (classItems, baseAttrType) = item;
                if (baseAttrType == null) return;

                var processedSymbols = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                foreach (var (classDecl, classSymbol) in classItems)
                {
                    if (classSymbol == null || !processedSymbols.Add(classSymbol))
                        continue;

                    // Collect all unique extension types contributed by applied attributes.
                    var extensions = new List<ITypeSymbol>();
                    var seenExtensions = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
                    foreach (var attr in classSymbol.GetAttributes())
                    {
                        var extensionType = FindExtensionType(attr.AttributeClass, baseAttrType);
                        if (extensionType != null && seenExtensions.Add(extensionType))
                            extensions.Add(extensionType);
                    }

                    if (extensions.Count == 0) continue;

                    bool isPartial = classDecl.Modifiers.Any(SyntaxKind.PartialKeyword);
                    if (!isPartial)
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(
                            MustBePartialDescriptor,
                            classSymbol.Locations.IsEmpty ? Location.None : classSymbol.Locations[0],
                            classSymbol.Name));
                        continue;
                    }

                    var source = GenerateSource(classSymbol, extensions);
                    var hintName = classSymbol.ContainingNamespace.IsGlobalNamespace
                        ? $"{classSymbol.MetadataName}.AgentExtensions.g.cs"
                        : $"{classSymbol.ContainingNamespace.ToDisplayString()}.{classSymbol.MetadataName}.AgentExtensions.g.cs";
                    spc.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
                }
            });
        }

        /// <summary>
        /// Walks the base-type chain of <paramref name="attrClass"/> looking for the closed
        /// construction of <paramref name="baseAttrType"/>. Returns the first type argument when
        /// found, or <c>null</c> if the attribute is not related to <c>AgentExtensionAttribute&lt;T&gt;</c>.
        /// </summary>
        private static ITypeSymbol? FindExtensionType(INamedTypeSymbol? attrClass, INamedTypeSymbol baseAttrType)
        {
            var current = attrClass;
            while (current != null)
            {
                if (current.IsGenericType &&
                    SymbolEqualityComparer.Default.Equals(current.ConstructedFrom, baseAttrType))
                {
                    return current.TypeArguments[0];
                }
                current = current.BaseType;
            }
            return null;
        }

        /// <summary>
        /// Derives a property name from the extension type name by stripping well-known suffixes:
        /// <c>AgentExtension</c> first, then <c>Extension</c>.
        /// </summary>
        private static string DerivePropertyName(ITypeSymbol extensionType)
        {
            var name = extensionType.Name;
            if (name.EndsWith("AgentExtension"))
                name = name.Substring(0, name.Length - "AgentExtension".Length);
            if (name.EndsWith("Extension"))
                name = name.Substring(0, name.Length - "Extension".Length);
            return name + "Extension";
        }

        private static string GenerateSource(INamedTypeSymbol classSymbol, List<ITypeSymbol> extensions)
        {
            var ns = classSymbol.ContainingNamespace?.IsGlobalNamespace == true
                ? null
                : classSymbol.ContainingNamespace?.ToDisplayString();

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("// Copyright (c) Microsoft Corporation. All rights reserved.");
            sb.AppendLine("// Licensed under the MIT License.");
            sb.AppendLine();
            sb.AppendLine("#nullable disable");
            sb.AppendLine();

            if (ns != null)
            {
                sb.AppendLine($"namespace {ns}");
                sb.AppendLine("{");
                AppendClass(sb, classSymbol, extensions, "    ");
                sb.AppendLine("}");
            }
            else
            {
                AppendClass(sb, classSymbol, extensions, "");
            }

            return sb.ToString();
        }

        private static void AppendClass(
            StringBuilder sb,
            INamedTypeSymbol classSymbol,
            List<ITypeSymbol> extensions,
            string indent)
        {
            sb.AppendLine($"{indent}partial class {classSymbol.Name}");
            sb.AppendLine($"{indent}{{");

            // Emit an auto-property with private set for each extension.
            foreach (var extensionType in extensions)
            {
                var fullTypeName = extensionType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var propName = DerivePropertyName(extensionType);

                sb.AppendLine($"{indent}    /// <summary>");
                sb.AppendLine($"{indent}    /// Provides access to <see cref=\"{extensionType.ToDisplayString()}\"/> agent features.");
                sb.AppendLine($"{indent}    /// </summary>");
                sb.AppendLine($"{indent}    public {fullTypeName} {propName} {{ get; private set; }}");
                sb.AppendLine();
            }

            // Emit a single ConfigureExtensions override that eagerly initializes all extensions.
            // This is called by AgentApplication's constructor so that extension OnBeforeTurn
            // handlers and other infrastructure are registered before the first turn arrives,
            // regardless of whether the agent accesses the extension properties directly.
            sb.AppendLine($"{indent}    protected override void ConfigureExtensions()");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        base.ConfigureExtensions();");
            foreach (var extensionType in extensions)
            {
                var fullTypeName = extensionType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var propName = DerivePropertyName(extensionType);

                sb.AppendLine($"{indent}        {propName} = new {fullTypeName}(this);");
                sb.AppendLine($"{indent}        RegisterExtension({propName}, _ => {{ }});");
            }
            sb.AppendLine($"{indent}    }}");

            sb.AppendLine($"{indent}}}");
        }
    }
}
