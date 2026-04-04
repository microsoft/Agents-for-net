// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.Extensions.Teams.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class TeamsRouteAttributeAnalyzer : DiagnosticAnalyzer
    {
        public const string ReturnTypeDiagnosticId = "MTEAMS001";
        public const string ParameterCountDiagnosticId = "MTEAMS002";
        public const string ParameterTypeDiagnosticId = "MTEAMS003";

        internal static readonly DiagnosticDescriptor ReturnTypeDescriptor = new(
            id: ReturnTypeDiagnosticId,
            title: "Wrong return type for Teams route attribute",
            messageFormat: "Method '{0}' decorated with '[{1}]' must return '{2}'",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor ParameterCountDescriptor = new(
            id: ParameterCountDiagnosticId,
            title: "Wrong parameter count for Teams route attribute",
            messageFormat: "Method '{0}' decorated with '[{1}]' must have {2} parameters",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor ParameterTypeDescriptor = new(
            id: ParameterTypeDiagnosticId,
            title: "Wrong parameter type for Teams route attribute",
            messageFormat: "Parameter {0} of method '{1}' decorated with '[{2}]' must be of type '{3}'",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

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

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(ReturnTypeDescriptor, ParameterCountDescriptor, ParameterTypeDescriptor, MutualExclusivityDescriptor, DuplicateCommandIdDescriptor, InvalidRegexDescriptor);

        // -----------------------------------------------------------------------------------------
        // Metadata names for shared parameter types
        // -----------------------------------------------------------------------------------------

        private const string TurnContext    = "Microsoft.Agents.Builder.ITurnContext";
        private const string TurnState      = "Microsoft.Agents.Builder.State.ITurnState";
        private const string CancelToken    = "System.Threading.CancellationToken";
        private const string Activity       = "Microsoft.Agents.Core.Models.IActivity";
        private const string StringType     = "System.String";
        private const string Response       = "Microsoft.Teams.Api.MessageExtensions.Response";
        private const string ActionResponse = "Microsoft.Teams.Api.MessageExtensions.ActionResponse";
        private const string Query          = "Microsoft.Teams.Api.MessageExtensions.Query";
        private const string MeetingDetails = "Microsoft.Teams.Api.Meetings.MeetingDetails";
        private const string ParticipantsDetails = "Microsoft.Agents.Extensions.Teams.Models.MeetingParticipantsEventDetails";
        private const string Channel = "Microsoft.Teams.Api.Channel";
        private const string Team    = "Microsoft.Teams.Api.Team";

        // -----------------------------------------------------------------------------------------
        // Mutual exclusivity — attributes where commandId and commandIdPattern are exclusive
        // -----------------------------------------------------------------------------------------

        private static readonly ImmutableHashSet<string> MutualExclusivityAttributeNames =
            ImmutableHashSet.Create(
                "Microsoft.Agents.Extensions.Teams.App.MessageExtensions.QueryRouteAttribute",
                "Microsoft.Agents.Extensions.Teams.App.MessageExtensions.FetchTaskRouteAttribute",
                "Microsoft.Agents.Extensions.Teams.App.MessageExtensions.MessagePreviewEditRouteAttribute",
                "Microsoft.Agents.Extensions.Teams.App.MessageExtensions.MessagePreviewSendRouteAttribute",
                "Microsoft.Agents.Extensions.Teams.App.MessageExtensions.SubmitActionRouteAttribute");

        private static readonly ImmutableDictionary<string, string> MutualExclusivityDisplayNames =
            ImmutableDictionary<string, string>.Empty
                .Add("Microsoft.Agents.Extensions.Teams.App.MessageExtensions.QueryRouteAttribute",              "QueryRoute")
                .Add("Microsoft.Agents.Extensions.Teams.App.MessageExtensions.FetchTaskRouteAttribute",          "FetchTaskRoute")
                .Add("Microsoft.Agents.Extensions.Teams.App.MessageExtensions.MessagePreviewEditRouteAttribute", "MessagePreviewEditRoute")
                .Add("Microsoft.Agents.Extensions.Teams.App.MessageExtensions.MessagePreviewSendRouteAttribute", "MessagePreviewSendRoute")
                .Add("Microsoft.Agents.Extensions.Teams.App.MessageExtensions.SubmitActionRouteAttribute",       "SubmitActionRoute");

        // -----------------------------------------------------------------------------------------
        // Rule table — one entry per attribute, describes the required method signature.
        // null in ParameterTypes means "accept any type" (generic TData parameter).
        // ReturnTypeGenericArgument == null means plain Task (non-generic).
        // -----------------------------------------------------------------------------------------

        private static readonly ImmutableArray<SignatureRule> Rules = ImmutableArray.Create(
            // MessageExtensions
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.MessageExtensions.QueryRouteAttribute",
                AttributeDisplayName   = "QueryRoute",
                ReturnTypeGenericArgument = Response,
                ReturnTypeDisplayName  = "Task<Microsoft.Teams.Api.MessageExtensions.Response>",
                ParameterTypes         = new string?[] { TurnContext, TurnState, Query, CancelToken },
            },
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.MessageExtensions.QueryLinkRouteAttribute",
                AttributeDisplayName   = "QueryLinkRoute",
                ReturnTypeGenericArgument = Response,
                ReturnTypeDisplayName  = "Task<Microsoft.Teams.Api.MessageExtensions.Response>",
                ParameterTypes         = new string?[] { TurnContext, TurnState, StringType, CancelToken },
            },
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.MessageExtensions.QueryUrlSettingRouteAttribute",
                AttributeDisplayName   = "QueryUrlSettingRoute",
                ReturnTypeGenericArgument = Response,
                ReturnTypeDisplayName  = "Task<Microsoft.Teams.Api.MessageExtensions.Response>",
                ParameterTypes         = new string?[] { TurnContext, TurnState, CancelToken },
            },
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.MessageExtensions.FetchTaskRouteAttribute",
                AttributeDisplayName   = "FetchTaskRoute",
                ReturnTypeGenericArgument = ActionResponse,
                ReturnTypeDisplayName  = "Task<Microsoft.Teams.Api.MessageExtensions.ActionResponse>",
                ParameterTypes         = new string?[] { TurnContext, TurnState, CancelToken },
            },
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.MessageExtensions.MessagePreviewEditRouteAttribute",
                AttributeDisplayName   = "MessagePreviewEditRoute",
                ReturnTypeGenericArgument = Response,
                ReturnTypeDisplayName  = "Task<Microsoft.Teams.Api.MessageExtensions.Response>",
                ParameterTypes         = new string?[] { TurnContext, TurnState, Activity, CancelToken },
            },
            // MessagePreviewSendRoute returns plain Task (not Task<T>)
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.MessageExtensions.MessagePreviewSendRouteAttribute",
                AttributeDisplayName   = "MessagePreviewSendRoute",
                ReturnTypeGenericArgument = null,
                ReturnTypeDisplayName  = "Task",
                ParameterTypes         = new string?[] { TurnContext, TurnState, Activity, CancelToken },
            },
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.MessageExtensions.ConfigureSettingsRouteAttribute",
                AttributeDisplayName   = "ConfigureSettingsRoute",
                ReturnTypeGenericArgument = Response,
                ReturnTypeDisplayName  = "Task<Microsoft.Teams.Api.MessageExtensions.Response>",
                ParameterTypes         = new string?[] { TurnContext, TurnState, Query, CancelToken },
            },
            // SubmitActionRoute: 3rd param is generic TData — accept any type
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.MessageExtensions.SubmitActionRouteAttribute",
                AttributeDisplayName   = "SubmitActionRoute",
                ReturnTypeGenericArgument = Response,
                ReturnTypeDisplayName  = "Task<Microsoft.Teams.Api.MessageExtensions.Response>",
                ParameterTypes         = new string?[] { TurnContext, TurnState, null, CancelToken },
            },
            // SelectItemRoute: 3rd param is generic TData — accept any type
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.MessageExtensions.SelectItemRouteAttribute",
                AttributeDisplayName   = "SelectItemRoute",
                ReturnTypeGenericArgument = Response,
                ReturnTypeDisplayName  = "Task<Microsoft.Teams.Api.MessageExtensions.Response>",
                ParameterTypes         = new string?[] { TurnContext, TurnState, null, CancelToken },
            },
            // CardButtonClickedRoute returns plain Task, 3rd param is generic TData
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.MessageExtensions.CardButtonClickedRouteAttribute",
                AttributeDisplayName   = "CardButtonClickedRoute",
                ReturnTypeGenericArgument = null,
                ReturnTypeDisplayName  = "Task",
                ParameterTypes         = new string?[] { TurnContext, TurnState, null, CancelToken },
            },
            // Meetings
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.Meetings.MeetingStartRouteAttribute",
                AttributeDisplayName   = "MeetingStartRoute",
                ReturnTypeGenericArgument = null,
                ReturnTypeDisplayName  = "Task",
                ParameterTypes         = new string?[] { TurnContext, TurnState, MeetingDetails, CancelToken },
            },
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.Meetings.MeetingEndRouteAttribute",
                AttributeDisplayName   = "MeetingEndRoute",
                ReturnTypeGenericArgument = null,
                ReturnTypeDisplayName  = "Task",
                ParameterTypes         = new string?[] { TurnContext, TurnState, MeetingDetails, CancelToken },
            },
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.Meetings.MeetingParticipantsJoinRouteAttribute",
                AttributeDisplayName   = "MeetingParticipantsJoinRoute",
                ReturnTypeGenericArgument = null,
                ReturnTypeDisplayName  = "Task",
                ParameterTypes         = new string?[] { TurnContext, TurnState, ParticipantsDetails, CancelToken },
            },
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.Meetings.MeetingParticipantsLeaveRouteAttribute",
                AttributeDisplayName   = "MeetingParticipantsLeaveRoute",
                ReturnTypeGenericArgument = null,
                ReturnTypeDisplayName  = "Task",
                ParameterTypes         = new string?[] { TurnContext, TurnState, ParticipantsDetails, CancelToken },
            },
            // TaskModules — 3rd param is either Request or a generic TData; accept any type
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.TaskModules.FetchRouteAttribute",
                AttributeDisplayName   = "FetchRoute",
                ReturnTypeGenericArgument = "Microsoft.Teams.Api.TaskModules.Response",
                ReturnTypeDisplayName  = "Task<Microsoft.Teams.Api.TaskModules.Response>",
                ParameterTypes         = new string?[] { TurnContext, TurnState, null, CancelToken },
            },
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.TaskModules.SubmitRouteAttribute",
                AttributeDisplayName   = "SubmitRoute",
                ReturnTypeGenericArgument = "Microsoft.Teams.Api.TaskModules.Response",
                ReturnTypeDisplayName  = "Task<Microsoft.Teams.Api.TaskModules.Response>",
                ParameterTypes         = new string?[] { TurnContext, TurnState, null, CancelToken },
            },
            // TeamsChannels — all return plain Task with (ITurnContext, ITurnState, Channel, CancellationToken)
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.TeamsChannels.ChannelCreatedRouteAttribute",
                AttributeDisplayName   = "ChannelCreatedRoute",
                ReturnTypeGenericArgument = null,
                ReturnTypeDisplayName  = "Task",
                ParameterTypes         = new string?[] { TurnContext, TurnState, Channel, CancelToken },
            },
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.TeamsChannels.ChannelDeletedRouteAttribute",
                AttributeDisplayName   = "ChannelDeletedRoute",
                ReturnTypeGenericArgument = null,
                ReturnTypeDisplayName  = "Task",
                ParameterTypes         = new string?[] { TurnContext, TurnState, Channel, CancelToken },
            },
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.TeamsChannels.ChannelMemberAddedRouteAttribute",
                AttributeDisplayName   = "ChannelMemberAddedRoute",
                ReturnTypeGenericArgument = null,
                ReturnTypeDisplayName  = "Task",
                ParameterTypes         = new string?[] { TurnContext, TurnState, Channel, CancelToken },
            },
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.TeamsChannels.ChannelMemberRemovedRouteAttribute",
                AttributeDisplayName   = "ChannelMemberRemovedRoute",
                ReturnTypeGenericArgument = null,
                ReturnTypeDisplayName  = "Task",
                ParameterTypes         = new string?[] { TurnContext, TurnState, Channel, CancelToken },
            },
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.TeamsChannels.ChannelRenamedRouteAttribute",
                AttributeDisplayName   = "ChannelRenamedRoute",
                ReturnTypeGenericArgument = null,
                ReturnTypeDisplayName  = "Task",
                ParameterTypes         = new string?[] { TurnContext, TurnState, Channel, CancelToken },
            },
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.TeamsChannels.ChannelRestoredRouteAttribute",
                AttributeDisplayName   = "ChannelRestoredRoute",
                ReturnTypeGenericArgument = null,
                ReturnTypeDisplayName  = "Task",
                ParameterTypes         = new string?[] { TurnContext, TurnState, Channel, CancelToken },
            },
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.TeamsChannels.ChannelSharedRouteAttribute",
                AttributeDisplayName   = "ChannelSharedRoute",
                ReturnTypeGenericArgument = null,
                ReturnTypeDisplayName  = "Task",
                ParameterTypes         = new string?[] { TurnContext, TurnState, Channel, CancelToken },
            },
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.TeamsChannels.ChannelUnSharedRouteAttribute",
                AttributeDisplayName   = "ChannelUnSharedRoute",
                ReturnTypeGenericArgument = null,
                ReturnTypeDisplayName  = "Task",
                ParameterTypes         = new string?[] { TurnContext, TurnState, Channel, CancelToken },
            },
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.TeamsChannels.ChannelUpdateRouteAttribute",
                AttributeDisplayName   = "ChannelUpdateRoute",
                ReturnTypeGenericArgument = null,
                ReturnTypeDisplayName  = "Task",
                ParameterTypes         = new string?[] { TurnContext, TurnState, Channel, CancelToken },
            },
            // TeamsTeams — all return plain Task with (ITurnContext, ITurnState, Team, CancellationToken)
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.TeamsTeams.TeamArchivedRouteAttribute",
                AttributeDisplayName   = "TeamArchivedRoute",
                ReturnTypeGenericArgument = null,
                ReturnTypeDisplayName  = "Task",
                ParameterTypes         = new string?[] { TurnContext, TurnState, Team, CancelToken },
            },
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.TeamsTeams.TeamUnarchivedRouteAttribute",
                AttributeDisplayName   = "TeamUnarchivedRoute",
                ReturnTypeGenericArgument = null,
                ReturnTypeDisplayName  = "Task",
                ParameterTypes         = new string?[] { TurnContext, TurnState, Team, CancelToken },
            },
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.TeamsTeams.TeamDeletedRouteAttribute",
                AttributeDisplayName   = "TeamDeletedRoute",
                ReturnTypeGenericArgument = null,
                ReturnTypeDisplayName  = "Task",
                ParameterTypes         = new string?[] { TurnContext, TurnState, Team, CancelToken },
            },
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.TeamsTeams.TeamHardDeletedRouteAttribute",
                AttributeDisplayName   = "TeamHardDeletedRoute",
                ReturnTypeGenericArgument = null,
                ReturnTypeDisplayName  = "Task",
                ParameterTypes         = new string?[] { TurnContext, TurnState, Team, CancelToken },
            },
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.TeamsTeams.TeamRenamedRouteAttribute",
                AttributeDisplayName   = "TeamRenamedRoute",
                ReturnTypeGenericArgument = null,
                ReturnTypeDisplayName  = "Task",
                ParameterTypes         = new string?[] { TurnContext, TurnState, Team, CancelToken },
            },
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.TeamsTeams.TeamRestoredRouteAttribute",
                AttributeDisplayName   = "TeamRestoredRoute",
                ReturnTypeGenericArgument = null,
                ReturnTypeDisplayName  = "Task",
                ParameterTypes         = new string?[] { TurnContext, TurnState, Team, CancelToken },
            },
            new SignatureRule
            {
                AttributeMetadataName  = "Microsoft.Agents.Extensions.Teams.App.TeamsTeams.TeamUpdateRouteAttribute",
                AttributeDisplayName   = "TeamUpdateRoute",
                ReturnTypeGenericArgument = null,
                ReturnTypeDisplayName  = "Task",
                ParameterTypes         = new string?[] { TurnContext, TurnState, Team, CancelToken },
            }
        );

        // -----------------------------------------------------------------------------------------
        // Analyzer implementation
        // -----------------------------------------------------------------------------------------

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(compilationCtx =>
            {
                // Resolve each attribute type symbol once per compilation. If none resolve,
                // Teams is not referenced and there is nothing to analyze.
                var attrToRule = new Dictionary<INamedTypeSymbol, SignatureRule>(SymbolEqualityComparer.Default);
                foreach (var rule in Rules)
                {
                    var sym = compilationCtx.Compilation.GetTypeByMetadataName(rule.AttributeMetadataName);
                    if (sym != null)
                        attrToRule[sym] = rule;
                }

                var attrToDisplayName = new Dictionary<INamedTypeSymbol, string>(SymbolEqualityComparer.Default);
                foreach (var name in MutualExclusivityAttributeNames)
                {
                    var sym = compilationCtx.Compilation.GetTypeByMetadataName(name);
                    if (sym != null)
                        attrToDisplayName[sym] = MutualExclusivityDisplayNames[name];
                }

                if (attrToRule.Count == 0 && attrToDisplayName.Count == 0)
                    return;

                compilationCtx.RegisterSymbolAction(
                    ctx => AnalyzeMethod(ctx, attrToRule, attrToDisplayName),
                    SymbolKind.Method);

                compilationCtx.RegisterSymbolAction(
                    ctx => AnalyzeClass(ctx, attrToDisplayName),
                    SymbolKind.NamedType);
            });
        }

        private static void AnalyzeMethod(
            SymbolAnalysisContext ctx,
            Dictionary<INamedTypeSymbol, SignatureRule> attrToRule,
            Dictionary<INamedTypeSymbol, string> attrToDisplayName)
        {
            var method = (IMethodSymbol)ctx.Symbol;

            foreach (var attribute in method.GetAttributes())
            {
                if (attribute.AttributeClass is null)
                    continue;

                if (!attrToRule.TryGetValue(attribute.AttributeClass, out var rule))
                    continue;

                var location = method.Locations.Length > 0 ? method.Locations[0] : Location.None;

                // 1. Return type
                if (!IsExpectedReturnType(method.ReturnType, rule, ctx.Compilation))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        ReturnTypeDescriptor,
                        location,
                        method.Name, rule.AttributeDisplayName, rule.ReturnTypeDisplayName));
                }

                // 2. Parameter count
                if (method.Parameters.Length != rule.ParameterTypes.Length)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        ParameterCountDescriptor,
                        location,
                        method.Name, rule.AttributeDisplayName, rule.ParameterTypes.Length));
                    return; // Parameter types can't be validated without correct count
                }

                // 3. Parameter types (null entry = accept any type)
                for (int i = 0; i < rule.ParameterTypes.Length; i++)
                {
                    if (rule.ParameterTypes[i] is null)
                        continue;

                    var expected = ctx.Compilation.GetTypeByMetadataName(rule.ParameterTypes[i]!);
                    if (expected is null)
                        continue; // Type not resolvable in this compilation — skip

                    if (!IsTypeCompatible(method.Parameters[i].Type, expected))
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(
                            ParameterTypeDescriptor,
                            location,
                            i + 1, method.Name, rule.AttributeDisplayName, rule.ParameterTypes[i]!));
                    }
                }
            }

            // MTEAMS004 / MTEAMS010 / MTEAMS011 checks
            var meLocation = method.Locations.Length > 0 ? method.Locations[0] : Location.None;
            foreach (var attribute in method.GetAttributes())
            {
                if (attribute.AttributeClass is null) continue;
                if (!attrToDisplayName.TryGetValue(attribute.AttributeClass, out var displayName)) continue;

                var args = attribute.ConstructorArguments;

                // ── commandId (args[0]) ──────────────────────────────────────
                if (args.Length < 1) continue;
                var commandId = args[0].Value as string;

                // [MTEAMS011 will be inserted here in Task 3]

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
        /// Returns true if <paramref name="actual"/> equals <paramref name="expected"/> or
        /// implements/extends it (handles concrete classes in place of interfaces).
        /// </summary>
        private static bool IsTypeCompatible(ITypeSymbol actual, ITypeSymbol expected)
        {
            if (SymbolEqualityComparer.Default.Equals(actual, expected))
                return true;

            // Allow a concrete implementation of an interface parameter
            if (expected.TypeKind == TypeKind.Interface)
            {
                foreach (var iface in actual.AllInterfaces)
                    if (SymbolEqualityComparer.Default.Equals(iface, expected))
                        return true;
            }

            return false;
        }

        private static bool IsExpectedReturnType(ITypeSymbol returnType, SignatureRule rule, Compilation compilation)
        {
            if (rule.ReturnTypeGenericArgument is null)
            {
                // Must be plain Task (non-generic)
                var taskType = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
                return taskType is not null
                    && SymbolEqualityComparer.Default.Equals(returnType, taskType);
            }
            else
            {
                // Must be Task<T>
                if (returnType is not INamedTypeSymbol named)
                    return false;

                var taskOfT = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");
                if (taskOfT is null)
                    return false;

                if (!SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, taskOfT))
                    return false;

                if (named.TypeArguments.Length != 1)
                    return false;

                var expectedArg = compilation.GetTypeByMetadataName(rule.ReturnTypeGenericArgument);
                if (expectedArg is null)
                    return false;

                return SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], expectedArg);
            }
        }
    }
}
