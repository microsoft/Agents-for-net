// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.Agents.Core.Analyzers
{
    /// <summary>
    /// Finds classes decorated with <c>[StreamingResponseFactory("channelId")]</c> and emits
    /// assembly-level <c>StreamingResponseFactoryAssemblyAttribute</c> entries so the runtime
    /// can auto-discover streaming response factories without explicit DI registration.
    /// </summary>
    [Generator]
    public class StreamingResponseFactorySourceGenerator : IIncrementalGenerator
    {
        internal const string MarkerAttributeFullName =
            "Microsoft.Agents.Builder.StreamingResponseFactoryAttribute";
        internal const string AssemblyAttributeFullName =
            "Microsoft.Agents.Builder.StreamingResponseFactoryAssemblyAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var factories = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    MarkerAttributeFullName,
                    (node, _) => node is ClassDeclarationSyntax,
                    (ctx, ct) =>
                    {
                        var symbol = ctx.TargetSymbol as INamedTypeSymbol;
                        if (symbol == null)
                            return ImmutableArray<(string TypeName, string ChannelId)>.Empty;

                        return symbol.GetAttributes()
                            .Where(a => a.AttributeClass?.ToDisplayString() == MarkerAttributeFullName
                                        && a.ConstructorArguments.Length == 1
                                        && a.ConstructorArguments[0].Value is string)
                            .Select(a => (
                                TypeName: symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                                ChannelId: (string)a.ConstructorArguments[0].Value!
                            ))
                            .ToImmutableArray();
                    })
                .Where(x => x.Length > 0)
                .SelectMany((entries, _) => entries)
                .Collect();

            context.RegisterSourceOutput(factories, static (spc, items) =>
            {
                if (items.IsDefaultOrEmpty)
                    return;

                var source = string.Join("\r\n", items.Distinct().Select(x =>
                    $"[assembly: {AssemblyAttributeFullName}(typeof({x.TypeName}), {Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(x.ChannelId, quote: true)})]"));

                spc.AddSource(
                    "StreamingResponseFactoryAssemblyAttributes.g.cs",
                    SourceText.From(source, Encoding.UTF8));
            });
        }
    }
}
