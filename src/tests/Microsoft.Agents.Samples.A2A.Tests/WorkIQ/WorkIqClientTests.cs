extern alias WorkIQBriefingSample;

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WorkIQBriefingSample::WorkIQBriefingAgent;

namespace Microsoft.Agents.Samples.A2A.Tests;

public class WorkIqClientTests
{
    [Fact]
    public async Task Run_AuthenticatesDiscoveryUsesV1AndProjectsStreamingArtifacts()
    {
        using var http = new SampleHttpClients((request, ct) => Task.FromResult(request.Method == HttpMethod.Get
            ? SampleHttpClients.Json(SampleHttpClients.Card())
            : SampleHttpClients.Stream(
                new StreamResponse { Task = SampleHttpClients.WorkIqTask() },
                Artifact("# Contoso\n", append: false),
                Artifact("Open commitment: proposal.", append: true),
                Completed())));
        var updates = new List<WorkIqProgress>();
        var client = CreateClient(http);

        var result = await client.RunAsync("Prepare a briefing.", "previous-workiq-context", null, "workiq-token",
            (update, ct) => { updates.Add(update); return Task.CompletedTask; }, CancellationToken.None);

        Assert.Equal("# Contoso\nOpen commitment: proposal.", result.Text);
        Assert.Equal("workiq-task", result.TaskId);
        Assert.Equal("workiq-context", result.ContextId);
        Assert.False(result.RequiresInput);
        Assert.Equal("# Contoso\nOpen commitment: proposal.", string.Concat(updates.Select(update => update.TextDelta)));
        Assert.All(http.Requests, request =>
        {
            Assert.Equal("workiq-token", request.Token);
            Assert.Equal("1.0", request.Version);
        });
        Assert.EndsWith("/a2a/.well-known/agent-card.json", http.Requests.First().Uri.AbsoluteUri);
        using var sent = JsonDocument.Parse(http.Requests.Last().Body);
        Assert.Equal("SendStreamingMessage", sent.RootElement.GetProperty("method").GetString());
        var message = sent.RootElement.GetProperty("params").GetProperty("message");
        Assert.Equal("previous-workiq-context", message.GetProperty("contextId").GetString());
        Assert.Equal("UTC", message.GetProperty("metadata").GetProperty("Location").GetProperty("timeZone").GetString());
    }

    [Fact]
    public async Task NonStreamingWorkingTask_IsRetrievedAndSubscribedWithoutSendingAgain()
    {
        using var http = new SampleHttpClients((request, ct) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return Task.FromResult(SampleHttpClients.Json(SampleHttpClients.Card(streaming: false)));
            }
            using var json = JsonDocument.Parse(request.Body);
            return Task.FromResult(json.RootElement.GetProperty("method").GetString() switch
            {
                "SendMessage" => SampleHttpClients.Rpc(request, new SendMessageResponse { Task = SampleHttpClients.WorkIqTask() }),
                "GetTask" => SampleHttpClients.Rpc(request, SampleHttpClients.WorkIqTask()),
                "SubscribeToTask" => SampleHttpClients.Stream(
                    new StreamResponse { Task = SampleHttpClients.WorkIqTask() },
                    Artifact("Briefing ready.", append: false), Completed()),
                _ => throw new InvalidOperationException("Unexpected downstream operation."),
            });
        });

        var result = await CreateClient(http).RunAsync("Prepare a briefing.", null, null, "workiq-token", IgnoreProgress, CancellationToken.None);

        Assert.Equal("Briefing ready.", result.Text);
        Assert.Equal(["SendMessage", "GetTask", "SubscribeToTask"], http.Requests
            .Where(request => request.Method == HttpMethod.Post)
            .Select(request => JsonDocument.Parse(request.Body).RootElement.GetProperty("method").GetString()!)
            .ToArray());
    }

    [Fact]
    public async Task ForeignDiscoveredEndpoint_IsRejectedBeforeDelegating()
    {
        using var http = new SampleHttpClients((request, ct) =>
            Task.FromResult(SampleHttpClients.Json(SampleHttpClients.Card(endpoint: "https://untrusted.example/a2a/"))));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateClient(http).RunAsync("Briefing", null, null, "workiq-token", IgnoreProgress, CancellationToken.None));

        Assert.Equal(HttpMethod.Get, Assert.Single(http.Requests).Method);
    }

    [Fact]
    public async Task WorkIqClarification_PreservesTheRemoteTaskForTheAnswer()
    {
        using var http = new SampleHttpClients((request, ct) => Task.FromResult(request.Method == HttpMethod.Get
            ? SampleHttpClients.Json(SampleHttpClients.Card(streaming: false))
            : SampleHttpClients.Rpc(request, new SendMessageResponse
            {
                Task = SampleHttpClients.WorkIqTask(TaskState.InputRequired, "Which Contoso meeting?"),
            })));

        var result = await CreateClient(http).RunAsync(
            "The 2 PM meeting.", "workiq-context", "pending-workiq-task", "workiq-token", IgnoreProgress, CancellationToken.None);

        Assert.True(result.RequiresInput);
        Assert.Equal("Which Contoso meeting?", result.Text);
        using var sent = JsonDocument.Parse(http.Requests.Last().Body);
        Assert.Equal("pending-workiq-task", sent.RootElement.GetProperty("params").GetProperty("message").GetProperty("taskId").GetString());
    }

    [Fact]
    public async Task ClarificationAfterAPartialArtifact_ReturnsTheQuestionInsteadOfTheDraft()
    {
        using var http = new SampleHttpClients((request, ct) => Task.FromResult(request.Method == HttpMethod.Get
            ? SampleHttpClients.Json(SampleHttpClients.Card())
            : SampleHttpClients.Stream(
                new StreamResponse { Task = SampleHttpClients.WorkIqTask() },
                Artifact("Partial briefing, not a question.", append: false),
                new StreamResponse
                {
                    StatusUpdate = new TaskStatusUpdateEvent
                    {
                        TaskId = "workiq-task",
                        ContextId = "workiq-context",
                        Status = new global::A2A.TaskStatus
                        {
                            State = TaskState.InputRequired,
                            Message = new Message { Role = Role.Agent, Parts = [Part.FromText("Which of the two Contoso meetings?")] },
                        },
                    },
                })));

        var result = await CreateClient(http).RunAsync("Prepare a briefing.", null, null, "workiq-token", IgnoreProgress, CancellationToken.None);

        Assert.True(result.RequiresInput);
        Assert.Equal("Which of the two Contoso meetings?", result.Text);
    }

    [Fact]
    public async Task Cancellation_AlsoRequestsCancellationOfTheKnownDownstreamTask()
    {
        using var cancellation = new CancellationTokenSource();
        using var http = new SampleHttpClients((request, ct) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return Task.FromResult(SampleHttpClients.Json(SampleHttpClients.Card()));
            }
            using var json = JsonDocument.Parse(request.Body);
            return Task.FromResult(json.RootElement.GetProperty("method").GetString() == "CancelTask"
                ? SampleHttpClients.Rpc(request, SampleHttpClients.WorkIqTask(TaskState.Canceled))
                : SampleHttpClients.Stream(new StreamResponse { Task = SampleHttpClients.WorkIqTask() }, Artifact("Partial", false)));
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateClient(http).RunAsync("Briefing", null, null, "workiq-token",
                (update, ct) => { cancellation.Cancel(); return Task.CompletedTask; }, cancellation.Token));

        var cancel = http.Requests.Last();
        using var requestBody = JsonDocument.Parse(cancel.Body);
        Assert.Equal("CancelTask", requestBody.RootElement.GetProperty("method").GetString());
        Assert.Equal("workiq-task", requestBody.RootElement.GetProperty("params").GetProperty("id").GetString());
    }

    private static WorkIqClient CreateClient(IHttpClientFactory factory)
        => new(factory, TimeZoneInfo.Utc, NullLogger<WorkIqClient>.Instance);

    private static Task IgnoreProgress(WorkIqProgress update, CancellationToken cancellationToken) => Task.CompletedTask;

    private static StreamResponse Artifact(string text, bool append) => new()
    {
        ArtifactUpdate = new TaskArtifactUpdateEvent
        {
            TaskId = "workiq-task",
            ContextId = "workiq-context",
            Append = append,
            Artifact = new Artifact { ArtifactId = "briefing", Parts = [Part.FromText(text)] },
        },
    };

    private static StreamResponse Completed() => new()
    {
        StatusUpdate = new TaskStatusUpdateEvent
        {
            TaskId = "workiq-task",
            ContextId = "workiq-context",
            Status = new global::A2A.TaskStatus { State = TaskState.Completed },
        },
    };
}
