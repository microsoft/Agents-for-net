// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.Agents.Core.Analyzers
{
    /// <summary>
    /// Emits an assembly-level StreamingResponseFactoryAssemblyAttribute for every type decorated with
    /// StreamingResponseFactoryAttribute, so per-channel IStreamingResponseFactory implementations are discovered
    /// automatically on module load (no explicit service-collection registration required).
    /// </summary>
    [Generator]
    public class StreamingResponseFactorySourceGenerator : IIncrementalGenerator
    {
        internal const string StreamingResponseFactoryAttributeFullName = "Microsoft.Agents.Builder.StreamingResponseFactoryAttribute";
        internal const string StreamingResponseFactoryAssemblyAttributeFullName = "Microsoft.Agents.Builder.StreamingResponseFactoryAssemblyAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            IncrementalValueProvider<ImmutableArray<string?>> types =
                context.SyntaxProvider
                    .ForAttributeWithMetadataName(
                        StreamingResponseFactoryAttributeFullName,
                        (node, _) => node is ClassDeclarationSyntax,
                        (context, ct) =>
                            (context.TargetSymbol as INamedTypeSymbol)?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    )
                    .Where(static x => x is not null)
                    .Collect();

            context.RegisterSourceOutput(types, static (context, types) =>
            {
                if (types.IsDefaultOrEmpty)
                {
                    return;
                }

                var source = string.Join("\r\n", types.Distinct().Select(x =>
                    $"[assembly: {StreamingResponseFactoryAssemblyAttributeFullName}(typeof({x}))]"));

                context.AddSource("StreamingResponseFactoryAssemblyAttributes.g.cs", SourceText.From(source, Encoding.UTF8));
            });
        }
    }
}
