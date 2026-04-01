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

namespace Microsoft.Agents.Extensions.Teams.Analyzers.Tests
{
    public class TeamsRouteAttributeAnalyzerTests
    {
        // ---------------------------------------------------------------------------
        // Helpers
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
                typeof(Microsoft.Agents.Extensions.Teams.App.MessageExtensions.QueryRouteAttribute).Assembly.Location);
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

        // ---------------------------------------------------------------------------
        // QueryRoute
        // ---------------------------------------------------------------------------

        private const string QueryRouteCorrect = """
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Agents.Builder;
            using Microsoft.Agents.Builder.State;

            public class Agent
            {
                [Microsoft.Agents.Extensions.Teams.App.MessageExtensions.QueryRoute("test")]
                public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnQuery(
                    ITurnContext ctx, ITurnState state,
                    Microsoft.Teams.Api.MessageExtensions.Query q,
                    CancellationToken ct) => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
            }
            """;

        [Fact]
        public async Task QueryRoute_CorrectSignature_NoDiagnostic()
        {
            var diagnostics = await GetDiagnosticsAsync(QueryRouteCorrect);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task QueryRoute_WrongReturnType_EmitsMTEAMS001()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.App.MessageExtensions.QueryRoute("test")]
                    public Task OnQuery(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.MessageExtensions.Query q,
                        CancellationToken ct) => Task.CompletedTask;
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            var d = Assert.Single(diagnostics);
            Assert.Equal(TeamsRouteAttributeAnalyzer.ReturnTypeDiagnosticId, d.Id);
            Assert.Contains("OnQuery", d.GetMessage());
            Assert.Contains("QueryRoute", d.GetMessage());
            Assert.Contains("Task<Microsoft.Teams.Api.MessageExtensions.Response>", d.GetMessage());
        }

        [Fact]
        public async Task QueryRoute_WrongParameterCount_EmitsMTEAMS002()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.App.MessageExtensions.QueryRoute("test")]
                    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnQuery(
                        ITurnContext ctx, ITurnState state,
                        CancellationToken ct)
                        => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            var d = Assert.Single(diagnostics);
            Assert.Equal(TeamsRouteAttributeAnalyzer.ParameterCountDiagnosticId, d.Id);
            Assert.Contains("OnQuery", d.GetMessage());
            Assert.Contains("QueryRoute", d.GetMessage());
            Assert.Contains("4", d.GetMessage());
        }

        [Fact]
        public async Task QueryRoute_WrongParameterType_EmitsMTEAMS003()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.App.MessageExtensions.QueryRoute("test")]
                    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnQuery(
                        ITurnContext ctx, ITurnState state,
                        string badParam,
                        CancellationToken ct)
                        => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            var d = Assert.Single(diagnostics);
            Assert.Equal(TeamsRouteAttributeAnalyzer.ParameterTypeDiagnosticId, d.Id);
            Assert.Contains("OnQuery", d.GetMessage());
            Assert.Contains("QueryRoute", d.GetMessage());
            Assert.Contains("Microsoft.Teams.Api.MessageExtensions.Query", d.GetMessage());
        }

        // ---------------------------------------------------------------------------
        // QueryLinkRoute
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task QueryLinkRoute_CorrectSignature_NoDiagnostic()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.App.MessageExtensions.QueryLinkRoute]
                    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnQueryLink(
                        ITurnContext ctx, ITurnState state, string url, CancellationToken ct)
                        => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task QueryLinkRoute_WrongReturnType_EmitsMTEAMS001()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.App.MessageExtensions.QueryLinkRoute]
                    public Task OnQueryLink(
                        ITurnContext ctx, ITurnState state, string url, CancellationToken ct)
                        => Task.CompletedTask;
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            var d = Assert.Single(diagnostics);
            Assert.Equal(TeamsRouteAttributeAnalyzer.ReturnTypeDiagnosticId, d.Id);
            Assert.Contains("OnQueryLink", d.GetMessage());
            Assert.Contains("QueryLinkRoute", d.GetMessage());
            Assert.Contains("Task<Microsoft.Teams.Api.MessageExtensions.Response>", d.GetMessage());
        }

        // ---------------------------------------------------------------------------
        // FetchTaskRoute
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task FetchTaskRoute_CorrectSignature_NoDiagnostic()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.App.MessageExtensions.FetchTaskRoute("cmd")]
                    public Task<Microsoft.Teams.Api.MessageExtensions.ActionResponse> OnFetchTask(
                        ITurnContext ctx, ITurnState state, CancellationToken ct)
                        => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.ActionResponse());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task FetchTaskRoute_WrongReturnType_EmitsMTEAMS001()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.App.MessageExtensions.FetchTaskRoute("cmd")]
                    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnFetchTask(
                        ITurnContext ctx, ITurnState state, CancellationToken ct)
                        => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            var d = Assert.Single(diagnostics);
            Assert.Equal(TeamsRouteAttributeAnalyzer.ReturnTypeDiagnosticId, d.Id);
            Assert.Contains("OnFetchTask", d.GetMessage());
            Assert.Contains("FetchTaskRoute", d.GetMessage());
            Assert.Contains("Task<Microsoft.Teams.Api.MessageExtensions.ActionResponse>", d.GetMessage());
        }

        // ---------------------------------------------------------------------------
        // MessagePreviewSendRoute (returns Task, not Task<T>)
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task MessagePreviewSendRoute_CorrectSignature_NoDiagnostic()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;
                using Microsoft.Agents.Core.Models;

                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.App.MessageExtensions.MessagePreviewSendRoute("cmd")]
                    public Task OnMessagePreviewSend(
                        ITurnContext ctx, ITurnState state, IActivity activity, CancellationToken ct)
                        => Task.CompletedTask;
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task MessagePreviewSendRoute_WrongReturnType_EmitsMTEAMS001()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;
                using Microsoft.Agents.Core.Models;

                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.App.MessageExtensions.MessagePreviewSendRoute("cmd")]
                    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnMessagePreviewSend(
                        ITurnContext ctx, ITurnState state, IActivity activity, CancellationToken ct)
                        => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            var d = Assert.Single(diagnostics);
            Assert.Equal(TeamsRouteAttributeAnalyzer.ReturnTypeDiagnosticId, d.Id);
            Assert.Contains("OnMessagePreviewSend", d.GetMessage());
            Assert.Contains("MessagePreviewSendRoute", d.GetMessage());
        }

        // ---------------------------------------------------------------------------
        // SubmitActionRoute (TData = any type, no type check on param 3)
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task SubmitActionRoute_CorrectSignature_NoDiagnostic()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using System.Collections.Generic;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.App.MessageExtensions.SubmitActionRoute("cmd")]
                    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnSubmitAction(
                        ITurnContext ctx, ITurnState state,
                        IDictionary<string, string> data,
                        CancellationToken ct)
                        => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task SubmitActionRoute_WrongReturnType_EmitsMTEAMS001()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using System.Collections.Generic;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.App.MessageExtensions.SubmitActionRoute("cmd")]
                    public Task OnSubmitAction(
                        ITurnContext ctx, ITurnState state,
                        IDictionary<string, string> data,
                        CancellationToken ct)
                        => Task.CompletedTask;
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            var d = Assert.Single(diagnostics);
            Assert.Equal(TeamsRouteAttributeAnalyzer.ReturnTypeDiagnosticId, d.Id);
            Assert.Contains("OnSubmitAction", d.GetMessage());
            Assert.Contains("SubmitActionRoute", d.GetMessage());
        }

        // ---------------------------------------------------------------------------
        // MeetingStartRoute
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task MeetingStartRoute_CorrectSignature_NoDiagnostic()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.App.Meetings.MeetingStartRoute]
                    public Task OnMeetingStart(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.Meetings.MeetingDetails meeting,
                        CancellationToken ct)
                        => Task.CompletedTask;
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task MeetingStartRoute_WrongReturnType_EmitsMTEAMS001()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.App.Meetings.MeetingStartRoute]
                    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnMeetingStart(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.Meetings.MeetingDetails meeting,
                        CancellationToken ct)
                        => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            var d = Assert.Single(diagnostics);
            Assert.Equal(TeamsRouteAttributeAnalyzer.ReturnTypeDiagnosticId, d.Id);
            Assert.Contains("OnMeetingStart", d.GetMessage());
            Assert.Contains("MeetingStartRoute", d.GetMessage());
        }

        [Fact]
        public async Task MeetingStartRoute_WrongParameterType_EmitsMTEAMS003()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.App.Meetings.MeetingStartRoute]
                    public Task OnMeetingStart(
                        ITurnContext ctx, ITurnState state,
                        string badParam,
                        CancellationToken ct)
                        => Task.CompletedTask;
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            var d = Assert.Single(diagnostics);
            Assert.Equal(TeamsRouteAttributeAnalyzer.ParameterTypeDiagnosticId, d.Id);
            Assert.Contains("OnMeetingStart", d.GetMessage());
            Assert.Contains("MeetingStartRoute", d.GetMessage());
            Assert.Contains("Microsoft.Teams.Api.Meetings.MeetingDetails", d.GetMessage());
        }

        // ---------------------------------------------------------------------------
        // MeetingParticipantsJoinRoute
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task MeetingParticipantsJoinRoute_CorrectSignature_NoDiagnostic()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;
                using Microsoft.Agents.Extensions.Teams.Models;

                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.App.Meetings.MeetingParticipantsJoinRoute]
                    public Task OnParticipantsJoin(
                        ITurnContext ctx, ITurnState state,
                        MeetingParticipantsEventDetails participants,
                        CancellationToken ct)
                        => Task.CompletedTask;
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        // ---------------------------------------------------------------------------
        // ConfigureSettingsRoute (returns Task, Query as 3rd param)
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task ConfigureSettingsRoute_CorrectSignature_NoDiagnostic()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.App.MessageExtensions.ConfigureSettingsRoute]
                    public Task OnConfigureSettings(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.MessageExtensions.Query settings,
                        CancellationToken ct)
                        => Task.CompletedTask;
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }
    }
}
