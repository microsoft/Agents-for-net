// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Analyzers.Extensions;
using Microsoft.Agents.Core.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace Microsoft.Agents.Core.Analyzers
{
    /// <summary>
    /// Forces referenced assemblies that contain custom <c>Microsoft.Agents.Core.Models.Entity</c>
    /// or <c>Microsoft.Agents.Core.Models.Activity</c> subclasses to load, so their
    /// <c>[SerializationInit]</c>/<c>[EntityInit]</c> registration runs before any (de)serialization.
    /// </summary>
    /// <remarks>
    /// Extension assemblies register custom Entity subclasses (via <c>EntityInitAssemblyAttribute</c>)
    /// and custom Activity subclasses (via <c>ActivityTypeInitAssemblyAttribute</c>). That registration
    /// only happens once the assembly is loaded, and the CLR does not load a referenced assembly until
    /// one of its types is first used. This generator scans the consuming compilation's referenced
    /// assemblies for such subclasses and emits a registry that references <c>typeof(...)</c> for each,
    /// forcing the owning assembly to load so its serialization initialization is triggered.
    /// </remarks>
    [Generator]
    [ExcludeFromCodeCoverage]
    public class PreloadAssembliesSourceGenerator : IIncrementalGenerator
    {
        internal const string EntityTypeFullName = "Microsoft.Agents.Core.Models.Entity";
        internal const string ActivityTypeFullName = "Microsoft.Agents.Core.Models.Activity";
        internal const string CoreModelsNamespacePrefix = "global::Microsoft.Agents.Core.Models";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Scan the referenced assemblies (not the current syntax) for Entity/Activity subclasses.
            var derivedTypesProvider =
                context.CompilationProvider
                    .Select(static (compilation, _) => FindDerivedTypes(compilation))
#pragma warning disable CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.
                    // Custom comparer expects string?, but we guarantee non-null strings in FindDerivedTypes.
                    .WithComparer(new ObjectImmutableArraySequenceEqualityComparer<string>());
#pragma warning restore CS8620

            context.RegisterSourceOutput(
                derivedTypesProvider,
                static (spc, derivedTypes) =>
                {
                    if (derivedTypes.IsDefaultOrEmpty)
                    {
                        return;
                    }

                    var source = GenerateSource(derivedTypes);
                    spc.AddSource("PreloadedAssemblies.g.cs", SourceText.From(source, Encoding.UTF8));
                });
        }

        private static ImmutableArray<string> FindDerivedTypes(Compilation compilation)
        {
            var baseTypes = new[]
            {
                compilation.GetTypeByMetadataName(EntityTypeFullName),
                compilation.GetTypeByMetadataName(ActivityTypeFullName),
            }.Where(static t => t is not null).ToImmutableArray();

            if (baseTypes.IsDefaultOrEmpty)
            {
                return ImmutableArray<string>.Empty;
            }

            var builder = ImmutableArray.CreateBuilder<string>();

            foreach (var assembly in compilation.References
                         .Select(compilation.GetAssemblyOrModuleSymbol)
                         .OfType<IAssemblySymbol>())
            {
                CollectDerivedTypes(assembly.GlobalNamespace, baseTypes, builder);
            }

            return builder.ToImmutable();
        }

        private static void CollectDerivedTypes(
            INamespaceSymbol ns,
            ImmutableArray<INamedTypeSymbol?> baseTypes,
            ImmutableArray<string>.Builder builder)
        {
            foreach (var member in ns.GetMembers())
            {
                if (member is INamespaceSymbol childNs)
                {
                    CollectDerivedTypes(childNs, baseTypes, builder);
                }
                else if (member is INamedTypeSymbol type
                    && type.TypeKind == TypeKind.Class
                    && !type.IsAbstract
                    && baseTypes.Any(baseType => type.InheritsFrom(baseType)))
                {
                    var name = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                    // The base types and the built-in subclasses live in Microsoft.Agents.Core.Models,
                    // which is always loaded and already registered — never preload it.
                    if (!name.StartsWith(CoreModelsNamespacePrefix))
                    {
                        builder.Add(name);
                    }
                }
            }
        }

        private static string GenerateSource(ImmutableArray<string> types)
        {
            var typesAsStrings = types.Distinct().Select(static x => $"typeof({x})");

            var sb = new StringBuilder();
            sb.AppendFormat(/* lang=c#-test */ """
            // <auto-generated />
            using System;
            using Microsoft.Agents.Core.Serialization;

            [assembly: Microsoft.Agents.Core.Serialization.SerializationInitAssemblyAttribute(typeof(global::PreloadTypesRegistry))]

            internal static class PreloadTypesRegistry
            {{
                private static readonly Type[] s_preloadedTypes;

                static PreloadTypesRegistry()
                {{
                    // Referencing typeof(...) forces each owning assembly to load, triggering its
                    // serialization initialization (EntityInit / SerializationInit).
                    s_preloadedTypes = new[]
                    {{
                        {0}
                    }};
                }}

                public static void Init()
                {{
                    _ = s_preloadedTypes.Length;
                }}
            }}
            """,
            string.Join(",\r\n            ", typesAsStrings));

            return sb.ToString();
        }
    }
}
