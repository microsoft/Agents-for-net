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
    /// Forces referenced assemblies that contain SDK extension types to load before feature-specific
    /// discovery runs.
    /// </summary>
    /// <remarks>
    /// The CLR does not load a referenced assembly until one of its types is first used. This generator
    /// scans the consuming compilation's referenced assemblies for custom Entity and Activity subclasses,
    /// channel-adapter manifests, and extension service-registration manifests. It emits a registry that
    /// references <c>typeof(...)</c> for each discovered type, forcing the owning assemblies to load.
    /// </remarks>
    [Generator]
    [ExcludeFromCodeCoverage]
    public class PreloadAssembliesSourceGenerator : IIncrementalGenerator
    {
        internal const string EntityTypeFullName = "Microsoft.Agents.Core.Models.Entity";
        internal const string ActivityTypeFullName = "Microsoft.Agents.Core.Models.Activity";
        internal const string CoreModelsNamespacePrefix = "global::Microsoft.Agents.Core.Models";
        internal const string ChannelAdapterInitAssemblyAttributeFullName = "Microsoft.Agents.Builder.Adapters.ChannelAdapterInitAssemblyAttribute";
        internal const string AgentServiceRegistrationAttributeFullName = "Microsoft.Agents.Hosting.AspNetCore.AgentServiceRegistrationAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var preloadTypesProvider =
                context.CompilationProvider
                    .Select(static (compilation, _) => FindPreloadTypes(compilation))
#pragma warning disable CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.
                    // Custom comparer expects string?, but FindPreloadTypes only returns non-null strings.
                    .WithComparer(new ObjectImmutableArraySequenceEqualityComparer<string>());
#pragma warning restore CS8620

            context.RegisterSourceOutput(
                preloadTypesProvider,
                static (spc, preloadTypes) =>
                {
                    if (preloadTypes.IsDefaultOrEmpty)
                    {
                        return;
                    }

                    var source = GenerateSource(preloadTypes);
                    spc.AddSource("PreloadedAssemblies.g.cs", SourceText.From(source, Encoding.UTF8));
                });
        }

        private static ImmutableArray<string> FindPreloadTypes(Compilation compilation)
        {
            var baseTypes = new[]
            {
                compilation.GetTypeByMetadataName(EntityTypeFullName),
                compilation.GetTypeByMetadataName(ActivityTypeFullName),
            }.Where(static t => t is not null).ToImmutableArray();

            var builder = ImmutableArray.CreateBuilder<string>();

            foreach (var assembly in compilation.References
                         .Select(compilation.GetAssemblyOrModuleSymbol)
                         .OfType<IAssemblySymbol>())
            {
                if (!baseTypes.IsDefaultOrEmpty)
                {
                    CollectDerivedTypes(assembly.GlobalNamespace, baseTypes, builder);
                }

                CollectManifestTypes(assembly, builder);
            }

            return builder.ToImmutable();
        }

        private static void CollectManifestTypes(IAssemblySymbol assembly, ImmutableArray<string>.Builder builder)
        {
            foreach (var attribute in assembly.GetAttributes())
            {
                var attributeName = attribute.AttributeClass?.ToDisplayString();
                if (attributeName != ChannelAdapterInitAssemblyAttributeFullName
                    && attributeName != AgentServiceRegistrationAttributeFullName)
                {
                    continue;
                }

                if (attribute.ConstructorArguments.Length > 0
                    && attribute.ConstructorArguments[0].Value is INamedTypeSymbol type
                    && IsExternallyAccessible(type))
                {
                    builder.Add(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                }
            }
        }

        private static bool IsExternallyAccessible(INamedTypeSymbol type)
        {
            for (var current = type; current != null; current = current.ContainingType)
            {
                if (current.DeclaredAccessibility != Accessibility.Public)
                {
                    return false;
                }
            }

            return true;
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
            [assembly: Microsoft.Agents.Core.AgentSdkInitAssemblyAttribute(typeof(global::PreloadTypesRegistry))]

            internal static class PreloadTypesRegistry
            {{
                private static readonly Type[] s_preloadedTypes;

                static PreloadTypesRegistry()
                {{
                    // Referencing typeof(...) forces each owning SDK extension assembly to load.
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
