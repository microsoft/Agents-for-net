// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.Agents.Core.Analyzers
{
    /// <summary>
    /// Scans referenced assemblies for known Agents SDK assembly-level attributes and emits
    /// a ModuleInitializer that forces those assemblies to load early. This ensures runtime
    /// discovery mechanisms (SerializationInit, EntityInit, StreamingResponseFactory) can find
    /// all registered types regardless of whether the app code directly references them.
    /// </summary>
    /// <remarks>
    /// Performance: Only checks assembly-level attributes on each reference (O(attributes) per assembly),
    /// not a full namespace/type tree walk. Typically completes in sub-millisecond time.
    /// </remarks>
    [Generator]
    public class PreloadAssembliesSourceGenerator : IIncrementalGenerator
    {
        // Assembly-level attributes that indicate an assembly has registered SDK types.
        // Any assembly with one of these needs to be force-loaded at startup.
        private static readonly string[] KnownAssemblyAttributes = new[]
        {
            "Microsoft.Agents.Core.Serialization.SerializationInitAssemblyAttribute",
            "Microsoft.Agents.Core.Serialization.EntityInitAssemblyAttribute",
            "Microsoft.Agents.Builder.StreamingResponseFactoryAssemblyAttribute",
        };

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var assembliesProvider = context.CompilationProvider
                .Select(static (compilation, ct) => FindAssembliesToPreload(compilation, ct));

            context.RegisterSourceOutput(assembliesProvider, static (spc, assemblies) =>
            {
                if (assemblies.IsDefaultOrEmpty)
                    return;

                spc.AddSource("AgentAssemblyPreloader.g.cs",
                    SourceText.From(GenerateSource(assemblies), Encoding.UTF8));
            });
        }

        private static ImmutableArray<string> FindAssembliesToPreload(
            Compilation compilation,
            System.Threading.CancellationToken ct)
        {
            var builder = ImmutableArray.CreateBuilder<string>();
            var compilationAssemblyName = compilation.AssemblyName;

            foreach (var reference in compilation.References)
            {
                ct.ThrowIfCancellationRequested();

                if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly)
                    continue;

                // Skip the compilation's own assembly and well-known framework assemblies
                var name = assembly.Identity.Name;
                if (name == compilationAssemblyName)
                    continue;
                if (IsFrameworkAssembly(name))
                    continue;

                // Check if this assembly has any known SDK assembly attributes
                if (!HasKnownAttribute(assembly))
                    continue;

                // Find one accessible type to use as the typeof() anchor
                var anchorType = FindAnchorType(assembly);
                if (anchorType != null)
                    builder.Add(anchorType);
            }

            return builder.ToImmutable();
        }

        private static bool IsFrameworkAssembly(string assemblyName)
        {
            return assemblyName.StartsWith("System")
                || assemblyName.StartsWith("Microsoft.AspNetCore")
                || assemblyName.StartsWith("Microsoft.Extensions")
                || assemblyName.StartsWith("Microsoft.CodeAnalysis")
                || assemblyName.StartsWith("Newtonsoft")
                || assemblyName == "mscorlib"
                || assemblyName == "netstandard";
        }

        private static bool HasKnownAttribute(IAssemblySymbol assembly)
        {
            foreach (var attr in assembly.GetAttributes())
            {
                var attrName = attr.AttributeClass?.ToDisplayString();
                if (attrName != null)
                {
                    for (int i = 0; i < KnownAssemblyAttributes.Length; i++)
                    {
                        if (attrName == KnownAssemblyAttributes[i])
                            return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Finds the first public, non-generic, non-nested type in the assembly to use
        /// as a typeof() anchor for forcing assembly load.
        /// </summary>
        private static string? FindAnchorType(IAssemblySymbol assembly)
        {
            return FindFirstPublicType(assembly.GlobalNamespace);
        }

        private static string? FindFirstPublicType(INamespaceSymbol ns)
        {
            foreach (var member in ns.GetMembers())
            {
                if (member is INamedTypeSymbol type &&
                    type.DeclaredAccessibility == Accessibility.Public &&
                    !type.IsGenericType &&
                    type.ContainingType == null)
                {
                    return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                }

                if (member is INamespaceSymbol childNs)
                {
                    var found = FindFirstPublicType(childNs);
                    if (found != null)
                        return found;
                }
            }
            return null;
        }

        private static string GenerateSource(ImmutableArray<string> types)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("// Forces referenced Agents SDK extension assemblies to load early");
            sb.AppendLine("// so runtime discovery (SerializationInit, EntityInit, StreamingResponseFactory) works.");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Runtime.CompilerServices;");
            sb.AppendLine();
            // Polyfill ModuleInitializerAttribute for netstandard2.0 targets
            sb.AppendLine("#if !NET5_0_OR_GREATER");
            sb.AppendLine("namespace System.Runtime.CompilerServices");
            sb.AppendLine("{");
            sb.AppendLine("    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]");
            sb.AppendLine("    internal sealed class ModuleInitializerAttribute : Attribute { }");
            sb.AppendLine("}");
            sb.AppendLine("#endif");
            sb.AppendLine();
            sb.AppendLine("internal static class AgentAssemblyPreloader");
            sb.AppendLine("{");
            sb.AppendLine("    private static readonly Type[] s_preloadedTypes = new Type[]");
            sb.AppendLine("    {");

            foreach (var type in types.Distinct())
            {
                sb.AppendLine($"        typeof({type}),");
            }

            sb.AppendLine("    };");
            sb.AppendLine();
            sb.AppendLine("    [ModuleInitializer]");
            sb.AppendLine("    internal static void Preload()");
            sb.AppendLine("    {");
            sb.AppendLine("        _ = s_preloadedTypes.Length;");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }
    }
}
