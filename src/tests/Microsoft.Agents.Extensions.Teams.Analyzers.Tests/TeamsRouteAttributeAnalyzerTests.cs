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
using Microsoft.Agents.Extensions.Teams.MessageExtensions;

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
                typeof(QueryRouteAttribute).Assembly.Location);
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

            [Microsoft.Agents.Extensions.Teams.TeamsExtension]
            public class Agent
            {
                [Microsoft.Agents.Extensions.Teams.MessageExtensions.QueryRoute("test")]
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

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.MessageExtensions.QueryRoute("test")]
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

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.MessageExtensions.QueryRoute("test")]
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

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.MessageExtensions.QueryRoute("test")]
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

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.MessageExtensions.QueryLinkRoute]
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

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.MessageExtensions.QueryLinkRoute]
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
        // FetchActionRoute
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task FetchActionRoute_CorrectSignature_NoDiagnostic()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.MessageExtensions.FetchActionRoute("cmd")]
                    public Task<Microsoft.Teams.Api.MessageExtensions.ActionResponse> OnFetchTask(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.MessageExtensions.Action action,
                        CancellationToken ct)
                        => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.ActionResponse());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task FetchActionRoute_WrongReturnType_EmitsMTEAMS001()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.MessageExtensions.FetchActionRoute("cmd")]
                    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnFetchTask(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.MessageExtensions.Action action,
                        CancellationToken ct)
                        => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            var d = Assert.Single(diagnostics);
            Assert.Equal(TeamsRouteAttributeAnalyzer.ReturnTypeDiagnosticId, d.Id);
            Assert.Contains("OnFetchTask", d.GetMessage());
            Assert.Contains("FetchActionRoute", d.GetMessage());
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

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.MessageExtensions.MessagePreviewSendRoute("cmd")]
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

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.MessageExtensions.MessagePreviewSendRoute("cmd")]
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
        // SubmitActionRoute (3rd param must be Action)
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task SubmitActionRoute_CorrectSignature_NoDiagnostic()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.MessageExtensions.SubmitActionRoute("cmd")]
                    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnSubmitAction(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.MessageExtensions.Action action,
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
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.MessageExtensions.SubmitActionRoute("cmd")]
                    public Task OnSubmitAction(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.MessageExtensions.Action action,
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

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.Meetings.MeetingStartRoute]
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

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.Meetings.MeetingStartRoute]
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

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.Meetings.MeetingStartRoute]
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

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.Meetings.MeetingParticipantsJoinRoute]
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

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.MessageExtensions.ConfigureSettingsRoute]
                    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnConfigureSettings(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.MessageExtensions.Query settings,
                        CancellationToken ct)
                        => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        // ---------------------------------------------------------------------------
        // TaskModules — TaskFetchRoute
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task TaskModules_FetchRoute_CorrectSignature_NoDiagnostic()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;
                using Microsoft.Agents.Extensions.Teams;

                [TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.TaskModules.TaskFetchRoute("myVerb")]
                    public Task<Microsoft.Teams.Api.TaskModules.Response> OnFetch(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.TaskModules.Request data,
                        CancellationToken ct)
                        => Task.FromResult(new Microsoft.Teams.Api.TaskModules.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task TaskModules_FetchRoute_WrongReturnType_EmitsMTEAMS001()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;
                using Microsoft.Agents.Extensions.Teams;

                [TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.TaskModules.TaskFetchRoute("myVerb")]
                    public Task OnFetch(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.TaskModules.Request data,
                        CancellationToken ct)
                        => Task.CompletedTask;
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            var d = Assert.Single(diagnostics);
            Assert.Equal(TeamsRouteAttributeAnalyzer.ReturnTypeDiagnosticId, d.Id);
            Assert.Contains("OnFetch", d.GetMessage());
            Assert.Contains("TaskFetchRoute", d.GetMessage());
            Assert.Contains("Task<Microsoft.Teams.Api.TaskModules.Response>", d.GetMessage());
        }

        [Fact]
        public async Task TaskModules_FetchRoute_WrongParameterCount_EmitsMTEAMS002()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;
                using Microsoft.Agents.Extensions.Teams;

                [TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.TaskModules.TaskFetchRoute("myVerb")]
                    public Task<Microsoft.Teams.Api.TaskModules.Response> OnFetch(
                        ITurnContext ctx, ITurnState state,
                        CancellationToken ct)
                        => Task.FromResult(new Microsoft.Teams.Api.TaskModules.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            var d = Assert.Single(diagnostics);
            Assert.Equal(TeamsRouteAttributeAnalyzer.ParameterCountDiagnosticId, d.Id);
            Assert.Contains("OnFetch", d.GetMessage());
            Assert.Contains("TaskFetchRoute", d.GetMessage());
            Assert.Contains("4", d.GetMessage());
        }

        [Fact]
        public async Task TaskModules_FetchRoute_TypedData_EmitsMTEAMS003()
        {
            // [TaskFetchRoute] requires Request as 3rd param
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;
                using Microsoft.Agents.Extensions.Teams;

                public record MyFetchData(string Name);

                [TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.TaskModules.TaskFetchRoute("myVerb")]
                    public Task<Microsoft.Teams.Api.TaskModules.Response> OnFetch(
                        ITurnContext ctx, ITurnState state,
                        MyFetchData data,
                        CancellationToken ct)
                        => Task.FromResult(new Microsoft.Teams.Api.TaskModules.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            var d = Assert.Single(diagnostics);
            Assert.Equal(TeamsRouteAttributeAnalyzer.ParameterTypeDiagnosticId, d.Id);
            Assert.Contains("OnFetch", d.GetMessage());
            Assert.Contains("TaskFetchRoute", d.GetMessage());
            Assert.Contains("Microsoft.Teams.Api.TaskModules.Request", d.GetMessage());
        }

        // ---------------------------------------------------------------------------
        // TaskModules — TaskSubmitRoute
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task TaskModules_SubmitRoute_CorrectSignature_NoDiagnostic()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;
                using Microsoft.Agents.Extensions.Teams;

                [TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.TaskModules.TaskSubmitRoute("myVerb")]
                    public Task<Microsoft.Teams.Api.TaskModules.Response> OnSubmit(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.TaskModules.Request data,
                        CancellationToken ct)
                        => Task.FromResult(new Microsoft.Teams.Api.TaskModules.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task TaskModules_SubmitRoute_TypedData_EmitsMTEAMS003()
        {
            // [TaskSubmitRoute] requires Request as 3rd param
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;
                using Microsoft.Agents.Extensions.Teams;

                public record MySubmitData(string Name, string Email);

                [TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.TaskModules.TaskSubmitRoute("myVerb")]
                    public Task<Microsoft.Teams.Api.TaskModules.Response> OnSubmit(
                        ITurnContext ctx, ITurnState state,
                        MySubmitData data,
                        CancellationToken ct)
                        => Task.FromResult(new Microsoft.Teams.Api.TaskModules.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            var d = Assert.Single(diagnostics);
            Assert.Equal(TeamsRouteAttributeAnalyzer.ParameterTypeDiagnosticId, d.Id);
            Assert.Contains("OnSubmit", d.GetMessage());
            Assert.Contains("TaskSubmitRoute", d.GetMessage());
            Assert.Contains("Microsoft.Teams.Api.TaskModules.Request", d.GetMessage());
        }

        [Fact]
        public async Task TaskModules_SubmitRoute_WrongReturnType_EmitsMTEAMS001()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;
                using Microsoft.Agents.Extensions.Teams;

                [TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.TaskModules.TaskSubmitRoute("myVerb")]
                    public Task OnSubmit(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.TaskModules.Request data,
                        CancellationToken ct)
                        => Task.CompletedTask;
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            var d = Assert.Single(diagnostics);
            Assert.Equal(TeamsRouteAttributeAnalyzer.ReturnTypeDiagnosticId, d.Id);
            Assert.Contains("OnSubmit", d.GetMessage());
            Assert.Contains("TaskSubmitRoute", d.GetMessage());
            Assert.Contains("Task<Microsoft.Teams.Api.TaskModules.Response>", d.GetMessage());
        }

        // ---------------------------------------------------------------------------
        // TeamsChannels — ChannelCreatedRoute
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task ChannelCreatedRoute_CorrectSignature_NoDiagnostic()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.TeamsChannels.ChannelCreatedRoute]
                    public Task OnChannelCreated(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.Channel channel,
                        CancellationToken ct)
                        => Task.CompletedTask;
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task ChannelCreatedRoute_WrongReturnType_EmitsMTEAMS001()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.TeamsChannels.ChannelCreatedRoute]
                    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnChannelCreated(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.Channel channel,
                        CancellationToken ct)
                        => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            var d = Assert.Single(diagnostics);
            Assert.Equal(TeamsRouteAttributeAnalyzer.ReturnTypeDiagnosticId, d.Id);
            Assert.Contains("OnChannelCreated", d.GetMessage());
            Assert.Contains("ChannelCreatedRoute", d.GetMessage());
        }

        [Fact]
        public async Task ChannelCreatedRoute_WrongParameterType_EmitsMTEAMS003()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.TeamsChannels.ChannelCreatedRoute]
                    public Task OnChannelCreated(
                        ITurnContext ctx, ITurnState state,
                        string badParam,
                        CancellationToken ct)
                        => Task.CompletedTask;
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            var d = Assert.Single(diagnostics);
            Assert.Equal(TeamsRouteAttributeAnalyzer.ParameterTypeDiagnosticId, d.Id);
            Assert.Contains("OnChannelCreated", d.GetMessage());
            Assert.Contains("ChannelCreatedRoute", d.GetMessage());
            Assert.Contains("Microsoft.Teams.Api.Channel", d.GetMessage());
        }

        // ---------------------------------------------------------------------------
        // TeamsTeams — TeamArchivedRoute
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task TeamArchivedRoute_CorrectSignature_NoDiagnostic()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.TeamsTeams.TeamArchivedRoute]
                    public Task OnTeamArchived(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.Team team,
                        CancellationToken ct)
                        => Task.CompletedTask;
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task TeamArchivedRoute_WrongReturnType_EmitsMTEAMS001()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.TeamsTeams.TeamArchivedRoute]
                    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnTeamArchived(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.Team team,
                        CancellationToken ct)
                        => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            var d = Assert.Single(diagnostics);
            Assert.Equal(TeamsRouteAttributeAnalyzer.ReturnTypeDiagnosticId, d.Id);
            Assert.Contains("OnTeamArchived", d.GetMessage());
            Assert.Contains("TeamArchivedRoute", d.GetMessage());
        }

        [Fact]
        public async Task TeamArchivedRoute_WrongParameterType_EmitsMTEAMS003()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.TeamsTeams.TeamArchivedRoute]
                    public Task OnTeamArchived(
                        ITurnContext ctx, ITurnState state,
                        string badParam,
                        CancellationToken ct)
                        => Task.CompletedTask;
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            var d = Assert.Single(diagnostics);
            Assert.Equal(TeamsRouteAttributeAnalyzer.ParameterTypeDiagnosticId, d.Id);
            Assert.Contains("OnTeamArchived", d.GetMessage());
            Assert.Contains("TeamArchivedRoute", d.GetMessage());
            Assert.Contains("Microsoft.Teams.Api.Team", d.GetMessage());
        }

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

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.MessageExtensions.QueryRoute("cmd", "cmd*")]
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
            Assert.Contains("QueryRoute", d.GetMessage());
        }

        [Fact]
        public async Task FetchActionRoute_BothCommandIdAndPattern_EmitsMTEAMS004()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.MessageExtensions.FetchActionRoute("cmd", "cmd*")]
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
            Assert.Contains("FetchActionRoute", d.GetMessage());
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

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.MessageExtensions.QueryRoute(null, "cmd*")]
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

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.MessageExtensions.QueryRoute("search")]
                    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnQuery1(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.MessageExtensions.Query q,
                        CancellationToken ct) => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());

                    [Microsoft.Agents.Extensions.Teams.MessageExtensions.QueryRoute("search")]
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
            Assert.Contains("QueryRoute", mteams009[0].GetMessage());
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

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.MessageExtensions.QueryRoute("search")]
                    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnQuery1(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.MessageExtensions.Query q,
                        CancellationToken ct) => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());

                    [Microsoft.Agents.Extensions.Teams.MessageExtensions.QueryRoute("lookup")]
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
            // QueryRoute("x") + FetchActionRoute("x") should NOT trigger MTEAMS009
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.MessageExtensions.QueryRoute("cmd")]
                    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnQuery(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.MessageExtensions.Query q,
                        CancellationToken ct) => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());

                    [Microsoft.Agents.Extensions.Teams.MessageExtensions.FetchActionRoute("cmd")]
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

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.MessageExtensions.QueryRoute(null, "[(invalid")]
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
            Assert.Contains("QueryRoute", mteams010[0].GetMessage());
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

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.MessageExtensions.QueryRoute(null, "search.*")]
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

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.MessageExtensions.SubmitActionRoute(null, "[bad")]
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

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.MessageExtensions.QueryRoute("")]
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
            Assert.Contains("QueryRoute", mteams011[0].GetMessage());
        }

        [Fact]
        public async Task QueryRoute_NullCommandId_NoMTEAMS011()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.MessageExtensions.QueryRoute(null, "search.*")]
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
        // MTEAMS012 — wrong Activity namespace (Teams.Api.Activities vs Core.Models.IActivity)
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task MessagePreviewEditRoute_TeamsApiActivity_EmitsMTEAMS012NotMTEAMS003()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.MessageExtensions.MessagePreviewEditRoute("cmd")]
                    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnEdit(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.Activities.Activity activityPreview,
                        CancellationToken ct) => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Contains(diagnostics, d => d.Id == TeamsRouteAttributeAnalyzer.TeamsActivityNamespaceDiagnosticId);
            Assert.DoesNotContain(diagnostics, d => d.Id == TeamsRouteAttributeAnalyzer.ParameterTypeDiagnosticId);
        }

        [Fact]
        public async Task MessagePreviewSendRoute_TeamsApiActivity_EmitsMTEAMS012()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.MessageExtensions.MessagePreviewSendRoute("cmd")]
                    public Task OnSend(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.Activities.Activity activityPreview,
                        CancellationToken ct) => Task.CompletedTask;
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Contains(diagnostics, d => d.Id == TeamsRouteAttributeAnalyzer.TeamsActivityNamespaceDiagnosticId);
            Assert.DoesNotContain(diagnostics, d => d.Id == TeamsRouteAttributeAnalyzer.ParameterTypeDiagnosticId);
        }

        [Fact]
        public async Task MessagePreviewEditRoute_CoreIActivity_NoDiagnostic()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;
                using Microsoft.Agents.Core.Models;

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.MessageExtensions.MessagePreviewEditRoute("cmd")]
                    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnEdit(
                        ITurnContext ctx, ITurnState state,
                        IActivity activityPreview,
                        CancellationToken ct) => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.DoesNotContain(diagnostics, d =>
                d.Id == TeamsRouteAttributeAnalyzer.TeamsActivityNamespaceDiagnosticId ||
                d.Id == TeamsRouteAttributeAnalyzer.ParameterTypeDiagnosticId);
        }

        [Fact]
        public async Task MessagePreviewEditRoute_TeamsApiMessageActivity_EmitsMTEAMS012()
        {
            // Teams.Api.Activities.MessageActivity inherits from Teams.Api.Activities.Activity —
            // confirmed via TeamsModelExtensions.cs and CoreVsTeamsModelPropertyTests.cs in this repo.
            // Should still trigger MTEAMS012
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;

                [Microsoft.Agents.Extensions.Teams.TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.MessageExtensions.MessagePreviewEditRoute("cmd")]
                    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnEdit(
                        ITurnContext ctx, ITurnState state,
                        Microsoft.Teams.Api.Activities.MessageActivity activityPreview,
                        CancellationToken ct) => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Contains(diagnostics, d => d.Id == TeamsRouteAttributeAnalyzer.TeamsActivityNamespaceDiagnosticId);
            Assert.DoesNotContain(diagnostics, d => d.Id == TeamsRouteAttributeAnalyzer.ParameterTypeDiagnosticId);
        }

        // ---------------------------------------------------------------------------
        // MTEAMS013 — TaskFetchRoute/TaskSubmitRoute without [TeamsExtension]
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
                using Microsoft.Agents.Extensions.Teams;

                [TeamsExtension]
                public partial class MyAgent : AgentApplication
                {
                    public MyAgent(AgentApplicationOptions options) : base(options) { }

                    [Microsoft.Agents.Extensions.Teams.TaskModules.TaskFetchRoute("myVerb")]
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
                using Microsoft.Agents.Extensions.Teams;

                [TeamsExtension]
                public partial class MyAgent : AgentApplication
                {
                    public MyAgent(AgentApplicationOptions options) : base(options) { }

                    [Microsoft.Agents.Extensions.Teams.TaskModules.TaskSubmitRoute("myVerb")]
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

                    [Microsoft.Agents.Extensions.Teams.TaskModules.TaskFetchRoute("myVerb")]
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
            Assert.Contains("TaskFetchRoute", d.GetMessage());
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

                    [Microsoft.Agents.Extensions.Teams.TaskModules.TaskSubmitRoute("myVerb")]
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
            Assert.Contains("TaskSubmitRoute", d.GetMessage());
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

                    [Microsoft.Agents.Extensions.Teams.MessageExtensions.QueryRoute("cmd")]
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
            Assert.Contains("QueryRoute", d.GetMessage());
            Assert.Contains("MyAgent", d.GetMessage());
        }

        // ---------------------------------------------------------------------------
        // Config — ConfigFetchRoute
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task Config_FetchRoute_CorrectSignature_NoDiagnostic()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;
                using Microsoft.Agents.Extensions.Teams;

                [TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.Configs.ConfigFetchRoute]
                    public Task<Microsoft.Teams.Api.Config.ConfigResponse> OnFetch(
                        ITurnContext ctx, ITurnState state,
                        object configData,
                        CancellationToken ct)
                        => Task.FromResult(new Microsoft.Teams.Api.Config.ConfigResponse());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task Config_FetchRoute_WrongReturnType_EmitsMTEAMS001()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;
                using Microsoft.Agents.Extensions.Teams;

                [TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.Configs.ConfigFetchRoute]
                    public Task OnFetch(
                        ITurnContext ctx, ITurnState state,
                        object configData,
                        CancellationToken ct)
                        => Task.CompletedTask;
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            var d = Assert.Single(diagnostics);
            Assert.Equal(TeamsRouteAttributeAnalyzer.ReturnTypeDiagnosticId, d.Id);
            Assert.Contains("OnFetch", d.GetMessage());
            Assert.Contains("ConfigFetchRoute", d.GetMessage());
            Assert.Contains("Task<Microsoft.Teams.Api.Config.ConfigResponse>", d.GetMessage());
        }

        [Fact]
        public async Task Config_FetchRoute_WrongParameterCount_EmitsMTEAMS002()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;
                using Microsoft.Agents.Extensions.Teams;

                [TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.Configs.ConfigFetchRoute]
                    public Task<Microsoft.Teams.Api.Config.ConfigResponse> OnFetch(
                        ITurnContext ctx, ITurnState state,
                        CancellationToken ct)
                        => Task.FromResult(new Microsoft.Teams.Api.Config.ConfigResponse());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            var d = Assert.Single(diagnostics);
            Assert.Equal(TeamsRouteAttributeAnalyzer.ParameterCountDiagnosticId, d.Id);
            Assert.Contains("OnFetch", d.GetMessage());
            Assert.Contains("ConfigFetchRoute", d.GetMessage());
            Assert.Contains("4", d.GetMessage());
        }

        // ---------------------------------------------------------------------------
        // Config — ConfigSubmitRoute
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task Config_SubmitRoute_CorrectSignature_NoDiagnostic()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;
                using Microsoft.Agents.Extensions.Teams;

                [TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.Configs.ConfigSubmitRoute]
                    public Task<Microsoft.Teams.Api.Config.ConfigResponse> OnSubmit(
                        ITurnContext ctx, ITurnState state,
                        object configData,
                        CancellationToken ct)
                        => Task.FromResult(new Microsoft.Teams.Api.Config.ConfigResponse());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task Config_SubmitRoute_WrongReturnType_EmitsMTEAMS001()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;
                using Microsoft.Agents.Extensions.Teams;

                [TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.Configs.ConfigSubmitRoute]
                    public Task OnSubmit(
                        ITurnContext ctx, ITurnState state,
                        object configData,
                        CancellationToken ct)
                        => Task.CompletedTask;
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            var d = Assert.Single(diagnostics);
            Assert.Equal(TeamsRouteAttributeAnalyzer.ReturnTypeDiagnosticId, d.Id);
            Assert.Contains("OnSubmit", d.GetMessage());
            Assert.Contains("ConfigSubmitRoute", d.GetMessage());
            Assert.Contains("Task<Microsoft.Teams.Api.Config.ConfigResponse>", d.GetMessage());
        }

        [Fact]
        public async Task Config_SubmitRoute_WrongParameterCount_EmitsMTEAMS002()
        {
            const string source = """
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;
                using Microsoft.Agents.Extensions.Teams;

                [TeamsExtension]
                public class Agent
                {
                    [Microsoft.Agents.Extensions.Teams.Configs.ConfigSubmitRoute]
                    public Task<Microsoft.Teams.Api.Config.ConfigResponse> OnSubmit(
                        ITurnContext ctx, ITurnState state,
                        CancellationToken ct)
                        => Task.FromResult(new Microsoft.Teams.Api.Config.ConfigResponse());
                }
                """;
            var diagnostics = await GetDiagnosticsAsync(source);
            var d = Assert.Single(diagnostics);
            Assert.Equal(TeamsRouteAttributeAnalyzer.ParameterCountDiagnosticId, d.Id);
            Assert.Contains("OnSubmit", d.GetMessage());
            Assert.Contains("ConfigSubmitRoute", d.GetMessage());
            Assert.Contains("4", d.GetMessage());
        }
    }
}
