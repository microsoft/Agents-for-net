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
    /// Emits an assembly <c>ChannelAdapterInitAssemblyAttribute</c> for every class annotated with
    /// <c>[ChannelAdapter("channelId")]</c>, so the <c>ChannelAdapterRegistry</c> can auto-register
    /// channel adapters at load time without scanning every type in the assembly. Mirrors the
    /// <c>ActivityTypeInitSourceGenerator</c> pattern.
    /// </summary>
    [Generator]
    public class ChannelAdapterInitSourceGenerator : IIncrementalGenerator
    {
        internal const string ChannelAdapterAttributeFullName = "Microsoft.Agents.Builder.Adapters.ChannelAdapterAttribute";
        internal const string ChannelAdapterInitAssemblyAttributeFullName = "Microsoft.Agents.Builder.Adapters.ChannelAdapterInitAssemblyAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            IncrementalValueProvider<ImmutableArray<string?>> lines =
                context.SyntaxProvider
                    .ForAttributeWithMetadataName(
                        ChannelAdapterAttributeFullName,
                        (node, _) => node is ClassDeclarationSyntax,
                        (ctx, _) => BuildAttributeLines(ctx))
                    .Where(static x => x is not null)
                    .Collect();

            context.RegisterSourceOutput(lines, static (context, lines) =>
            {
                if (lines.IsDefaultOrEmpty)
                {
                    return;
                }

                var source = string.Join("\r\n", lines.Distinct());
                context.AddSource("ChannelAdapterInitAssemblyAttributes.g.cs", SourceText.From(source, Encoding.UTF8));
            });
        }

        private static string? BuildAttributeLines(GeneratorAttributeSyntaxContext ctx)
        {
            if (ctx.TargetSymbol is not INamedTypeSymbol symbol)
            {
                return null;
            }

            var typeName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            var builder = new StringBuilder();
            foreach (var attribute in ctx.Attributes)
            {
                if (attribute.ConstructorArguments.Length < 1)
                {
                    continue;
                }

                if (attribute.ConstructorArguments[0].Value is not string channelId || string.IsNullOrEmpty(channelId))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append("\r\n");
                }

                builder.Append("[assembly: ")
                    .Append(ChannelAdapterInitAssemblyAttributeFullName)
                    .Append("(typeof(")
                    .Append(typeName)
                    .Append("), ")
                    .Append(SymbolDisplay.FormatLiteral(channelId, true))
                    .Append(")]");
            }

            return builder.Length > 0 ? builder.ToString() : null;
        }
    }
}
