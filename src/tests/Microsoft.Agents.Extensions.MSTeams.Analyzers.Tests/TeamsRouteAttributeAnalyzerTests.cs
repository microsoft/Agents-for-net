// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Agents.Extensions.MSTeams.MessageExtensions;

namespace Microsoft.Agents.Extensions.MSTeams.Analyzers.Tests
{
    public class TeamsRouteAttributeAnalyzerTests
    {
        // ---------------------------------------------------------------------------
        // Helpers
        //
        // NOTE: Handler-signature validation (formerly MTEAMS001/002/003/012) has been
        // migrated to the generic MAA002 analyzer (RouteHandlerSignatureAnalyzer) driven
        // by [RouteHandlerType] on each Teams route attribute. This analyzer now only
        // enforces Teams-specific semantic rules: MTEAMS004/009/010/011/013.
        // ---------------------------------------------------------------------------

        /// <summary>
        /// Returns all trusted platform assemblies (BCL) plus the Teams-specific
        /// assemblies needed to compile source that references route attributes.
        /// </summary>
        private static IEnumerable<MetadataReference> GetAllReferences()
        {
            // All BCL / framework assemblies trusted in this process
            var trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            if (trusted != null)
            {
                foreach (var path in trusted.Split(Path.PathSeparator))
                    if (File.Exists(path))
                        yield return MetadataReference.CreateFromFile(path);
            }

            // Teams extension (contains the route attributes)
            yield return MetadataReference.CreateFromFile(
                typeof(TeamsQueryRouteAttribute).Assembly.Location);
            // Builder (ITurnContext, ITurnState, AgentApplication…)
            yield return MetadataReference.CreateFromFile(
                typeof(Microsoft.Agents.Builder.ITurnContext).Assembly.Location);
            // Core models (IActivity)
            yield return MetadataReference.CreateFromFile(
                typeof(Microsoft.Agents.Core.Models.IActivity).Assembly.Location);
            // Teams.Api (Query, Response, ActionResponse, MeetingDetails…)
            yield return MetadataReference.CreateFromFile(
                typeof(Microsoft.Teams.Api.MessageExtensions.Query).Assembly.Location);
        }

        private static async Task<IReadOnlyList<Diagnostic>> GetDiagnosticsAsync(string source)
        {
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                new[] { CSharpSyntaxTree.ParseText(source) },
                GetAllReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new TeamsRouteAttributeAnalyzer());
            var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);
            return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
        }

        private const string QueryRouteCorrect = """
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Agents.Builder;
            using Microsoft.Agents.Builder.State;

            [Microsoft.Agents.Extensions.MSTeams.TeamsExtension]
            public class Agent
            {
                [Microsoft.Agents.Extensions.MSTeams.MessageExtensions.TeamsQueryRoute("test")]
                public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnQuery(
                    ITurnContext ctx, ITurnState state,
                    Microsoft.Teams.Api.MessageExtensions.Query q,
                    CancellationToken ct) => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
            }
            """;

        // ---------------------------------------------------------------------------
        // MTEAMS004 — mutual exclusivity (commandId + commandIdPattern)
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task QueryRoute_BothCommandIdAndPattern_EmitsMTEAMS004()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                [Microsoft.Agents.Extensions.MSTeams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.MSTeams.MessageExtensions.TeamsQueryRoute("cmd", "cmd*")]
                    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnQuery(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.MessageExtensions.Query q,
                        CancellationToken ct)
                        => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            var d = Assert.Single(diagnostics);
            Assert.Equal(TeamsRouteAttributeAnalyzer.MutualExclusivityDiagnosticId, d.Id);
            Assert.Contains("OnQuery", d.GetMessage());
            Assert.Contains("TeamsQueryRoute", d.GetMessage());
        }

        [Fact]
        public async Task FetchActionRoute_BothCommandIdAndPattern_EmitsMTEAMS004()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                [Microsoft.Agents.Extensions.MSTeams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.MSTeams.MessageExtensions.TeamsFetchActionRoute("cmd", "cmd*")]
                    public Task<Microsoft.Teams.Api.MessageExtensions.ActionResponse> OnFetchTask(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.MessageExtensions.Action action,
                        CancellationToken ct)
                        => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.ActionResponse());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            var d = Assert.Single(diagnostics);
            Assert.Equal(TeamsRouteAttributeAnalyzer.MutualExclusivityDiagnosticId, d.Id);
            Assert.Contains("OnFetchTask", d.GetMessage());
            Assert.Contains("TeamsFetchActionRoute", d.GetMessage());
        }

        [Fact]
        public async Task QueryRoute_OnlyCommandId_NoDiagnostic()
        {
            var diagnostics = await GetDiagnosticsAsync(QueryRouteCorrect); // commandId only
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task QueryRoute_OnlyCommandIdPattern_NoDiagnostic()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                [Microsoft.Agents.Extensions.MSTeams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.MSTeams.MessageExtensions.TeamsQueryRoute(null, "cmd*")]
                    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnQuery(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.MessageExtensions.Query q,
                        CancellationToken ct)
                        => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        // ---------------------------------------------------------------------------
        // MTEAMS009 — duplicate commandId in same class
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task QueryRoute_DuplicateCommandId_EmitsMTEAMS009()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                [Microsoft.Agents.Extensions.MSTeams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.MSTeams.MessageExtensions.TeamsQueryRoute("search")]
                    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnQuery1(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.MessageExtensions.Query q,
                        CancellationToken ct) => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());

                    [Microsoft.Agents.Extensions.MSTeams.MessageExtensions.TeamsQueryRoute("search")]
                    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnQuery2(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.MessageExtensions.Query q,
                        CancellationToken ct) => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            var mteams009 = diagnostics.Where(d => d.Id == TeamsRouteAttributeAnalyzer.DuplicateCommandIdDiagnosticId).ToList();
            Assert.Single(mteams009);
            Assert.Contains("OnQuery2", mteams009[0].GetMessage());
            Assert.Contains("TeamsQueryRoute", mteams009[0].GetMessage());
            Assert.Contains("search", mteams009[0].GetMessage());
            Assert.Contains("OnQuery1", mteams009[0].GetMessage());
        }

        [Fact]
        public async Task QueryRoute_DifferentCommandIds_NoDuplicateDiagnostic()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                [Microsoft.Agents.Extensions.MSTeams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.MSTeams.MessageExtensions.TeamsQueryRoute("search")]
                    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnQuery1(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.MessageExtensions.Query q,
                        CancellationToken ct) => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());

                    [Microsoft.Agents.Extensions.MSTeams.MessageExtensions.TeamsQueryRoute("lookup")]
                    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnQuery2(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.MessageExtensions.Query q,
                        CancellationToken ct) => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.DoesNotContain(diagnostics, d => d.Id == TeamsRouteAttributeAnalyzer.DuplicateCommandIdDiagnosticId);
        }

        [Fact]
        public async Task DifferentAttributeTypes_SameCommandId_NoDuplicateDiagnostic()
        {
            // TeamsQueryRoute("x") + TeamsFetchActionRoute("x") should NOT trigger MTEAMS009
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                [Microsoft.Agents.Extensions.MSTeams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.MSTeams.MessageExtensions.TeamsQueryRoute("cmd")]
                    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnQuery(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.MessageExtensions.Query q,
                        CancellationToken ct) => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());

                    [Microsoft.Agents.Extensions.MSTeams.MessageExtensions.TeamsFetchActionRoute("cmd")]
                    public Task<Microsoft.Teams.Api.MessageExtensions.ActionResponse> OnFetch(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.MessageExtensions.Action action,
                        CancellationToken ct) => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.ActionResponse());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.DoesNotContain(diagnostics, d => d.Id == TeamsRouteAttributeAnalyzer.DuplicateCommandIdDiagnosticId);
        }

        // ---------------------------------------------------------------------------
        // MTEAMS010 — invalid regex in commandIdPattern
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task QueryRoute_InvalidCommandIdPattern_EmitsMTEAMS010()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                [Microsoft.Agents.Extensions.MSTeams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.MSTeams.MessageExtensions.TeamsQueryRoute(null, "[(invalid")]
                    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnQuery(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.MessageExtensions.Query q,
                        CancellationToken ct) => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            var mteams010 = diagnostics.Where(d => d.Id == TeamsRouteAttributeAnalyzer.InvalidRegexDiagnosticId).ToList();
            Assert.Single(mteams010);
            Assert.Contains("OnQuery", mteams010[0].GetMessage());
            Assert.Contains("TeamsQueryRoute", mteams010[0].GetMessage());
            Assert.Contains("[(invalid", mteams010[0].GetMessage());
        }

        [Fact]
        public async Task QueryRoute_ValidCommandIdPattern_NoDiagnostic()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                [Microsoft.Agents.Extensions.MSTeams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.MSTeams.MessageExtensions.TeamsQueryRoute(null, "search.*")]
                    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnQuery(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.MessageExtensions.Query q,
                        CancellationToken ct) => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.DoesNotContain(diagnostics, d => d.Id == TeamsRouteAttributeAnalyzer.InvalidRegexDiagnosticId);
        }

        [Fact]
        public async Task SubmitActionRoute_InvalidCommandIdPattern_EmitsMTEAMS010()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                [Microsoft.Agents.Extensions.MSTeams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.MSTeams.MessageExtensions.TeamsSubmitActionRoute(null, "[bad")]
                    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnSubmit(
                        ITurnContext ctx, ITurnState state, string data, CancellationToken ct)
                        => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Contains(diagnostics, d => d.Id == TeamsRouteAttributeAnalyzer.InvalidRegexDiagnosticId);
        }

        // ---------------------------------------------------------------------------
        // MTEAMS011 — empty commandId string
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task QueryRoute_EmptyCommandId_EmitsMTEAMS011()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                [Microsoft.Agents.Extensions.MSTeams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.MSTeams.MessageExtensions.TeamsQueryRoute("")]
                    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnQuery(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.MessageExtensions.Query q,
                        CancellationToken ct) => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            var mteams011 = diagnostics.Where(d => d.Id == TeamsRouteAttributeAnalyzer.EmptyCommandIdDiagnosticId).ToList();
            Assert.Single(mteams011);
            Assert.Contains("OnQuery", mteams011[0].GetMessage());
            Assert.Contains("TeamsQueryRoute", mteams011[0].GetMessage());
        }

        [Fact]
        public async Task QueryRoute_NullCommandId_NoMTEAMS011()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                [Microsoft.Agents.Extensions.MSTeams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.MSTeams.MessageExtensions.TeamsQueryRoute(null, "search.*")]
                    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnQuery(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.MessageExtensions.Query q,
                        CancellationToken ct) => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.DoesNotContain(diagnostics, d => d.Id == TeamsRouteAttributeAnalyzer.EmptyCommandIdDiagnosticId);
        }

        // ---------------------------------------------------------------------------
        // MTEAMS013 — Teams route attribute used without [TeamsExtension]
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task FetchRoute_WithTeamsExtension_NoMTEAMS013()
        {
            // Happy path: [TeamsExtension] present — no MTEAMS013
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;
                using Microsoft.Agents.Builder.App;
                using Microsoft.Agents.Extensions.MSTeams;

                [TeamsExtension]
                public partial class MyAgent : AgentApplication
                {
                    public MyAgent(AgentApplicationOptions options) : base(options) { }

                    [Microsoft.Agents.Extensions.MSTeams.TaskModules.TeamsTaskFetchRoute("myVerb")]
                    public Task<Microsoft.Teams.Api.TaskModules.Response> OnFetch(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.TaskModules.Request data,
                        CancellationToken ct)
                        => Task.FromResult(new Microsoft.Teams.Api.TaskModules.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error
                                                    && d.Id.StartsWith("CS"));
            Assert.DoesNotContain(diagnostics,
                d => d.Id == TeamsRouteAttributeAnalyzer.MissingTeamsExtensionDiagnosticId);
        }

        [Fact]
        public async Task SubmitRoute_WithTeamsExtension_NoMTEAMS013()
        {
            // Happy path: [TeamsExtension] present — no MTEAMS013
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;
                using Microsoft.Agents.Builder.App;
                using Microsoft.Agents.Extensions.MSTeams;

                [TeamsExtension]
                public partial class MyAgent : AgentApplication
                {
                    public MyAgent(AgentApplicationOptions options) : base(options) { }

                    [Microsoft.Agents.Extensions.MSTeams.TaskModules.TeamsTaskSubmitRoute("myVerb")]
                    public Task<Microsoft.Teams.Api.TaskModules.Response> OnSubmit(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.TaskModules.Request data,
                        CancellationToken ct)
                        => Task.FromResult(new Microsoft.Teams.Api.TaskModules.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error
                                                    && d.Id.StartsWith("CS"));
            Assert.DoesNotContain(diagnostics,
                d => d.Id == TeamsRouteAttributeAnalyzer.MissingTeamsExtensionDiagnosticId);
        }

        [Fact]
        public async Task FetchRoute_WithoutTeamsExtension_EmitsMTEAMS013()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;
                using Microsoft.Agents.Builder.App;

                public class MyAgent : AgentApplication
                {
                    public MyAgent(AgentApplicationOptions options) : base(options) { }

                    [Microsoft.Agents.Extensions.MSTeams.TaskModules.TeamsTaskFetchRoute("myVerb")]
                    public Task<Microsoft.Teams.Api.TaskModules.Response> OnFetch(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.TaskModules.Request data,
                        CancellationToken ct)
                        => Task.FromResult(new Microsoft.Teams.Api.TaskModules.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            var d = Assert.Single(diagnostics,
                d => d.Id == TeamsRouteAttributeAnalyzer.MissingTeamsExtensionDiagnosticId);
            Assert.Contains("OnFetch", d.GetMessage());
            Assert.Contains("TeamsTaskFetchRoute", d.GetMessage());
            Assert.Contains("MyAgent", d.GetMessage());
        }

        [Fact]
        public async Task SubmitRoute_WithoutTeamsExtension_EmitsMTEAMS013()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;
                using Microsoft.Agents.Builder.App;

                public class MyAgent : AgentApplication
                {
                    public MyAgent(AgentApplicationOptions options) : base(options) { }

                    [Microsoft.Agents.Extensions.MSTeams.TaskModules.TeamsTaskSubmitRoute("myVerb")]
                    public Task<Microsoft.Teams.Api.TaskModules.Response> OnSubmit(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.TaskModules.Request data,
                        CancellationToken ct)
                        => Task.FromResult(new Microsoft.Teams.Api.TaskModules.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            var d = Assert.Single(diagnostics,
                d => d.Id == TeamsRouteAttributeAnalyzer.MissingTeamsExtensionDiagnosticId);
            Assert.Contains("OnSubmit", d.GetMessage());
            Assert.Contains("TeamsTaskSubmitRoute", d.GetMessage());
            Assert.Contains("MyAgent", d.GetMessage());
        }

        [Fact]
        public async Task QueryRoute_WithoutTeamsExtension_EmitsMTEAMS013()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;
                using Microsoft.Agents.Builder.App;

                public class MyAgent : AgentApplication
                {
                    public MyAgent(AgentApplicationOptions options) : base(options) { }

                    [Microsoft.Agents.Extensions.MSTeams.MessageExtensions.TeamsQueryRoute("cmd")]
                    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnQuery(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.MessageExtensions.Query q,
                        CancellationToken ct)
                        => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            var d = Assert.Single(diagnostics,
                d => d.Id == TeamsRouteAttributeAnalyzer.MissingTeamsExtensionDiagnosticId);
            Assert.Contains("OnQuery", d.GetMessage());
            Assert.Contains("TeamsQueryRoute", d.GetMessage());
            Assert.Contains("MyAgent", d.GetMessage());
        }
    }
}
