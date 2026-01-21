// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Analyzers.Extensions;
using Microsoft.Agents.Core.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Microsoft.Agents.Core.Analyzers
{
    /// <summary>
    /// Creates assembly EntityInitAssemblyAttribute to register all Entity subclasses for serialization.
    /// </summary>
    [Generator]
    public class EntityInitSourceGenerator : IIncrementalGenerator
    {
        internal const string EntityTypeFullName = "Microsoft.Agents.Core.Models.Entity";
        internal const string EntityInitAssemblyAttributeFullName = "Microsoft.Agents.Core.Serialization.EntityInitAssemblyAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            if (!Debugger.IsAttached)
            {
                //Debugger.Launch();
            }

            // Step 1: get the Compilation
            var compilationProvider = context.CompilationProvider;

            // Step 2: resolve Entity type
            var myTypeProvider = compilationProvider
                .Select(static (compilation, _) => compilation.GetTypeByMetadataName(EntityTypeFullName));

            // Step 3: find derived types
            var derivedTypesProvider =
                compilationProvider
                    .Combine(myTypeProvider)
                    .Select(static (pair, ct) => FindDerivedTypes(pair.Left, pair.Right))
#pragma warning disable CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.
                    // Custom comparer expects string?, but we guarantee non-null strings in FindDerivedTypes.
                    .WithComparer(new ObjectImmutableArraySequenceEqualityComparer<string>());
#pragma warning restore CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.

            // Step 4: generate EntityInitAssemblyAttributes for found derived types
            context.RegisterSourceOutput(
                derivedTypesProvider,
                static (spc, derivedTypes) =>
                {
                    if (derivedTypes.IsDefaultOrEmpty)
                    {
                        return;
                    }

                    var entityAttributes = string.Join("\r\n", derivedTypes.Distinct().Select(x => $"[assembly: {EntityInitAssemblyAttributeFullName}(typeof({x}))]"));
                    spc.AddSource("EntityInitAssemblyAttribute.g.cs", SourceText.From(entityAttributes, Encoding.UTF8));
                });
        }

        private static ImmutableArray<string> FindDerivedTypes(
            Compilation compilation,
            INamedTypeSymbol? myType)
        {
            if (myType is null)
                return ImmutableArray<string>.Empty;

            var builder = ImmutableArray.CreateBuilder<string>();

            CollectAnyDerivedType(compilation.Assembly.GlobalNamespace, myType, builder);

            return builder.ToImmutable();
        }

        private static void CollectAnyDerivedType(
            INamespaceSymbol ns,
            INamedTypeSymbol baseType,
            ImmutableArray<string>.Builder builder)
        {
            foreach (var member in ns.GetMembers())
            {
                if (member is INamespaceSymbol childNs)
                {
                    CollectAnyDerivedType(childNs, baseType, builder);
                }
                else if (member is INamedTypeSymbol type)
                {
                    if (type.InheritsFrom(baseType))
                    {
                        builder.Add(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                    }
                }
            }
        }
    }
}