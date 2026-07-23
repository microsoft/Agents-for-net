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
    /// Emits an assembly <c>ActivityTypeInitAssemblyAttribute</c> for every class annotated with
    /// <c>[ActivityType]</c>, so <c>ProtocolJsonSerializer</c> can auto-register custom Activity
    /// subclasses at load time without scanning every type in the assembly.
    /// </summary>
    [Generator]
    public class ActivityTypeInitSourceGenerator : IIncrementalGenerator
    {
        internal const string ActivityTypeAttributeFullName = "Microsoft.Agents.Core.Serialization.ActivityTypeAttribute";
        internal const string ActivityTypeInitAssemblyAttributeFullName = "Microsoft.Agents.Core.Serialization.ActivityTypeInitAssemblyAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            IncrementalValueProvider<ImmutableArray<string?>> types =
                context.SyntaxProvider
                    .ForAttributeWithMetadataName(
                        ActivityTypeAttributeFullName,
                        (node, _) => node is ClassDeclarationSyntax,
                        (ctx, ct) =>
                            (ctx.TargetSymbol as INamedTypeSymbol)?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                    .Where(static x => x is not null)
                    .Collect();

            context.RegisterSourceOutput(types, static (context, types) =>
            {
                if (types.IsDefaultOrEmpty)
                {
                    return;
                }

                var source = string.Join("\r\n", types.Distinct().Select(x =>
                    $"[assembly: {ActivityTypeInitAssemblyAttributeFullName}(typeof({x}))]"));

                context.AddSource("ActivityTypeInitAssemblyAttributes.g.cs", SourceText.From(source, Encoding.UTF8));
            });
        }
    }
}
