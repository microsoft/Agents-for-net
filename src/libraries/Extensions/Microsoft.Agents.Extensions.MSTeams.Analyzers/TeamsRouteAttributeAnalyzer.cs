// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.Extensions.MSTeams.Analyzers
{
    /// <summary>
    /// Analyzer for Teams route attributes that validates Teams-specific semantics that cannot be expressed
    /// through the handler delegate signature.
    /// </summary>
    /// <remarks>
    /// Handler signature validation for Teams route attributes is performed by the generic
    /// <c>Microsoft.Agents.Core.Analyzers.RouteHandlerSignatureAnalyzer</c> (MAA002). Each Teams route
    /// attribute declares its expected handler delegate via <c>[RouteHandlerType(typeof(...))]</c>, so this
    /// analyzer only covers the remaining Teams-specific rules: mutual exclusivity of <c>commandId</c>/
    /// <c>commandIdPattern</c>, duplicate <c>commandId</c> in a class, invalid/empty <c>commandId</c> values,
    /// and the requirement that Teams route attributes are used on a class decorated with <c>[TeamsExtension]</c>.
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class TeamsRouteAttributeAnalyzer : DiagnosticAnalyzer
    {
        public const string MutualExclusivityDiagnosticId = "MTEAMS004";

        internal static readonly DiagnosticDescriptor MutualExclusivityDescriptor = new(
            id: MutualExclusivityDiagnosticId,
            title: "Mutually exclusive attribute arguments",
            messageFormat: "Method '{0}' decorated with '[{1}]' cannot specify both 'commandId' and 'commandIdPattern' — they are mutually exclusive",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public const string DuplicateCommandIdDiagnosticId = "MTEAMS009";

        internal static readonly DiagnosticDescriptor DuplicateCommandIdDescriptor = new(
            id: DuplicateCommandIdDiagnosticId,
            title: "Duplicate commandId for Teams route attribute in same class",
            messageFormat: "Method '{0}' decorated with '[{1}]' uses commandId '{2}' which is already handled by method '{3}' in the same class",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public const string InvalidRegexDiagnosticId = "MTEAMS010";

        internal static readonly DiagnosticDescriptor InvalidRegexDescriptor = new(
            id: InvalidRegexDiagnosticId,
            title: "Invalid regex in commandIdPattern for Teams route attribute",
            messageFormat: "Method '{0}' decorated with '[{1}]' has an invalid commandIdPattern '{2}': {3}",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public const string EmptyCommandIdDiagnosticId = "MTEAMS011";

        internal static readonly DiagnosticDescriptor EmptyCommandIdDescriptor = new(
            id: EmptyCommandIdDiagnosticId,
            title: "Empty commandId string for Teams route attribute",
            messageFormat: "Method '{0}' decorated with '[{1}]' has an empty commandId string — did you mean null (match-all) instead?",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public const string MissingTeamsExtensionDiagnosticId = "MTEAMS013";

        internal static readonly DiagnosticDescriptor MissingTeamsExtensionDescriptor = new(
            id: MissingTeamsExtensionDiagnosticId,
            title: "Teams route attribute used without [TeamsExtension]",
            messageFormat: "Method '{0}' decorated with '[{1}]' is in class '{2}' which does not have '[TeamsExtension]' applied",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(
                MutualExclusivityDescriptor, DuplicateCommandIdDescriptor, InvalidRegexDescriptor,
                EmptyCommandIdDescriptor, MissingTeamsExtensionDescriptor);

        private const string RouteAttributeInterfaceMetadataName = "Microsoft.Agents.Builder.App.IRouteAttribute";
        private const string TeamsExtensionAttributeMetadataName = "Microsoft.Agents.Extensions.MSTeams.TeamsExtensionAttribute";
        private const string TeamsNamespacePrefix = "Microsoft.Agents.Extensions.MSTeams.";

        // -----------------------------------------------------------------------------------------
        // Mutual exclusivity — attributes where commandId and commandIdPattern are exclusive
        // -----------------------------------------------------------------------------------------

        private static readonly ImmutableHashSet<string> MutualExclusivityAttributeNames =
            ImmutableHashSet.Create(
                "Microsoft.Agents.Extensions.MSTeams.MessageExtensions.TeamsQueryRouteAttribute",
                "Microsoft.Agents.Extensions.MSTeams.MessageExtensions.TeamsFetchActionRouteAttribute",
                "Microsoft.Agents.Extensions.MSTeams.MessageExtensions.TeamsMessagePreviewEditRouteAttribute",
                "Microsoft.Agents.Extensions.MSTeams.MessageExtensions.TeamsMessagePreviewSendRouteAttribute",
                "Microsoft.Agents.Extensions.MSTeams.MessageExtensions.TeamsSubmitActionRouteAttribute");

        private static readonly ImmutableDictionary<string, string> MutualExclusivityDisplayNames =
            ImmutableDictionary<string, string>.Empty
                .Add("Microsoft.Agents.Extensions.MSTeams.MessageExtensions.TeamsQueryRouteAttribute",              "TeamsQueryRoute")
                .Add("Microsoft.Agents.Extensions.MSTeams.MessageExtensions.TeamsFetchActionRouteAttribute",        "TeamsFetchActionRoute")
                .Add("Microsoft.Agents.Extensions.MSTeams.MessageExtensions.TeamsMessagePreviewEditRouteAttribute", "TeamsMessagePreviewEditRoute")
                .Add("Microsoft.Agents.Extensions.MSTeams.MessageExtensions.TeamsMessagePreviewSendRouteAttribute", "TeamsMessagePreviewSendRoute")
                .Add("Microsoft.Agents.Extensions.MSTeams.MessageExtensions.TeamsSubmitActionRouteAttribute",       "TeamsSubmitActionRoute");

        // -----------------------------------------------------------------------------------------
        // Analyzer implementation
        // -----------------------------------------------------------------------------------------

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(compilationCtx =>
            {
                var routeInterface = compilationCtx.Compilation
                    .GetTypeByMetadataName(RouteAttributeInterfaceMetadataName);

                // If IRouteAttribute isn't referenced, Teams routing isn't in play — nothing to analyze.
                if (routeInterface is null)
                    return;

                var attrToDisplayName = new Dictionary<INamedTypeSymbol, string>(SymbolEqualityComparer.Default);
                foreach (var name in MutualExclusivityAttributeNames)
                {
                    var sym = compilationCtx.Compilation.GetTypeByMetadataName(name);
                    if (sym != null)
                        attrToDisplayName[sym] = MutualExclusivityDisplayNames[name];
                }

                var teamsExtensionAttrType = compilationCtx.Compilation
                    .GetTypeByMetadataName(TeamsExtensionAttributeMetadataName);

                compilationCtx.RegisterSymbolAction(
                    ctx => AnalyzeMethod(ctx, routeInterface, attrToDisplayName, teamsExtensionAttrType),
                    SymbolKind.Method);

                compilationCtx.RegisterSymbolAction(
                    ctx => AnalyzeClass(ctx, attrToDisplayName),
                    SymbolKind.NamedType);
            });
        }

        private static void AnalyzeMethod(
            SymbolAnalysisContext ctx,
            INamedTypeSymbol routeInterface,
            Dictionary<INamedTypeSymbol, string> attrToDisplayName,
            INamedTypeSymbol? teamsExtensionAttrType)
        {
            var method = (IMethodSymbol)ctx.Symbol;

            // MTEAMS004 / MTEAMS010 / MTEAMS011 — commandId / commandIdPattern checks
            var meLocation = method.Locations.Length > 0 ? method.Locations[0] : Location.None;
            foreach (var attribute in method.GetAttributes())
            {
                if (attribute.AttributeClass is null) continue;
                if (!attrToDisplayName.TryGetValue(attribute.AttributeClass, out var displayName)) continue;

                var args = attribute.ConstructorArguments;

                // ── commandId (args[0]) ──────────────────────────────────────
                if (args.Length < 1) continue;
                var commandId = args[0].Value as string;

                // MTEAMS011 — empty commandId (not null, but "")
                if (commandId != null && commandId.Length == 0)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        EmptyCommandIdDescriptor,
                        meLocation,
                        method.Name, displayName));
                }

                // ── commandIdPattern (args[1]) — only present if explicitly written ──
                if (args.Length < 2) continue;
                var commandIdPattern = args[1].Value as string;

                // MTEAMS010 — invalid regex in commandIdPattern
                if (!string.IsNullOrWhiteSpace(commandIdPattern))
                {
                    try { _ = new Regex(commandIdPattern); }
                    catch (ArgumentException ex)
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(
                            InvalidRegexDescriptor,
                            meLocation,
                            method.Name, displayName, commandIdPattern, ex.Message));
                    }
                }

                // MTEAMS004 — mutual exclusivity of commandId + commandIdPattern
                if (!string.IsNullOrWhiteSpace(commandId) && !string.IsNullOrWhiteSpace(commandIdPattern))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        MutualExclusivityDescriptor,
                        meLocation,
                        method.Name, displayName));
                }
            }

            // MTEAMS013 — Teams route attribute used on class without [TeamsExtension]
            if (teamsExtensionAttrType is null) return;

            // [TeamsExtensionAttribute] has Inherited = false — check only the immediate containing type.
            // Lazily computed: same answer for every attribute on this method.
            bool? hasTeamsExtension = null;

            foreach (var attribute in method.GetAttributes())
            {
                if (attribute.AttributeClass is null) continue;
                if (!IsTeamsRouteAttribute(attribute.AttributeClass, routeInterface)) continue;

                hasTeamsExtension ??= method.ContainingType.GetAttributes()
                    .Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, teamsExtensionAttrType));

                if (!hasTeamsExtension.Value)
                {
                    var location = method.Locations.Length > 0 ? method.Locations[0] : Location.None;
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        MissingTeamsExtensionDescriptor,
                        location,
                        method.Name, RouteDisplayName(attribute.AttributeClass), method.ContainingType.Name));
                }
            }
        }

        private static void AnalyzeClass(
            SymbolAnalysisContext ctx,
            Dictionary<INamedTypeSymbol, string> attrToDisplayName)
        {
            var type = (INamedTypeSymbol)ctx.Symbol;

            // attr symbol → commandId → first method that claimed it
            var seen = new Dictionary<INamedTypeSymbol, Dictionary<string, IMethodSymbol>>(
                SymbolEqualityComparer.Default);

            foreach (var member in type.GetMembers().OfType<IMethodSymbol>())
            {
                foreach (var attribute in member.GetAttributes())
                {
                    if (attribute.AttributeClass is null) continue;
                    if (!attrToDisplayName.TryGetValue(attribute.AttributeClass, out var displayName)) continue;

                    var args = attribute.ConstructorArguments;
                    if (args.Length < 1) continue;
                    var commandId = args[0].Value as string;
                    if (string.IsNullOrWhiteSpace(commandId)) continue;

                    if (!seen.TryGetValue(attribute.AttributeClass, out var cmdMap))
                    {
                        cmdMap = new Dictionary<string, IMethodSymbol>(StringComparer.Ordinal);
                        seen[attribute.AttributeClass] = cmdMap;
                    }

                    if (cmdMap.TryGetValue(commandId!, out var firstMethod))
                    {
                        var location = member.Locations.Length > 0 ? member.Locations[0] : Location.None;
                        ctx.ReportDiagnostic(Diagnostic.Create(
                            DuplicateCommandIdDescriptor,
                            location,
                            member.Name, displayName, commandId!, firstMethod.Name));
                    }
                    else
                    {
                        cmdMap[commandId!] = member;
                    }
                }
            }
        }

        /// <summary>
        /// Returns true if <paramref name="attributeClass"/> is a Teams route attribute — i.e. it implements
        /// <c>IRouteAttribute</c> and is declared in the Teams extension namespace.
        /// </summary>
        private static bool IsTeamsRouteAttribute(INamedTypeSymbol attributeClass, INamedTypeSymbol routeInterface)
        {
            if (!attributeClass.AllInterfaces.Any(i => i.Equals(routeInterface, SymbolEqualityComparer.Default)))
                return false;

            var ns = attributeClass.ContainingNamespace?.ToDisplayString();
            return ns != null && ns.StartsWith(TeamsNamespacePrefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// Produces the user-facing route name for an attribute class (its name without the
        /// trailing <c>Attribute</c> suffix), e.g. <c>TeamsQueryRouteAttribute</c> → <c>TeamsQueryRoute</c>.
        /// </summary>
        private static string RouteDisplayName(INamedTypeSymbol attributeClass)
        {
            var name = attributeClass.Name;
            return name.EndsWith("Attribute", StringComparison.Ordinal)
                ? name.Substring(0, name.Length - "Attribute".Length)
                : name;
        }
    }
}
