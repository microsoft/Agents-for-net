// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using A2A;
using Microsoft.Agents.Samples.A2AClient;
using Moq;
using Xunit;

namespace Microsoft.Agents.Samples.A2A.Tests;

/// <summary>
/// Drives the interactive loop end to end with scripted input, covering the request-failure and
/// authentication-mode paths that the command-only tests do not reach.
/// </summary>
public class A2AConsoleLoopTests
{
    [Fact]
    public async Task RunAsync_FailedSend_ReportsErrorAndKeepsAcceptingCommands()
    {
        var client = new Mock<IA2AClient>(MockBehavior.Strict);
        client.Setup(value => value.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new A2AException("agent unavailable", A2AErrorCode.InternalError));

        var output = new StringWriter();
        A2AConsole console = CreateConsole(client.Object, new A2AAuthenticationSession(), output, "hello", "", ":auth app", ":q");

        int exitCode = await console.RunAsync(CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("Request failed", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("agent unavailable", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Authentication mode: app", output.ToString(), StringComparison.Ordinal);
        Assert.False(console.IsRunning);
    }

    [Fact]
    public async Task RunAsync_FailedHistoryRead_DoesNotEndTheSession()
    {
        var client = new Mock<IA2AClient>(MockBehavior.Strict);
        client.Setup(value => value.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResponse(TaskState.Completed));
        client.Setup(value => value.GetTaskAsync(It.IsAny<GetTaskRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection reset"));

        var output = new StringWriter();
        A2AConsole console = CreateConsole(
            client.Object,
            new A2AAuthenticationSession(),
            output,
            showHistory: true,
            "hello", "", ":history off", ":q");

        int exitCode = await console.RunAsync(CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("Request failed", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("History display: off", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_CancellationDuringSend_IsNotSwallowed()
    {
        using var cancellation = new CancellationTokenSource();
        var client = new Mock<IA2AClient>(MockBehavior.Strict);
        client.Setup(value => value.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Returns<SendMessageRequest, CancellationToken>((_, token) =>
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
                return Task.FromResult(CreateResponse(TaskState.Completed));
            });

        var output = new StringWriter();
        A2AConsole console = CreateConsole(client.Object, new A2AAuthenticationSession(), output, "hello", "", ":q");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => console.RunAsync(cancellation.Token));

        Assert.DoesNotContain("Request failed", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_AuthModeChange_ClearsContinuationTask()
    {
        var requests = new List<SendMessageRequest>();
        var client = new Mock<IA2AClient>(MockBehavior.Strict);
        client.Setup(value => value.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SendMessageRequest, CancellationToken>((request, _) => requests.Add(request))
            .ReturnsAsync(CreateResponse(TaskState.InputRequired));

        var session = new A2AAuthenticationSession();
        var output = new StringWriter();
        A2AConsole console = CreateConsole(client.Object, session, output, "first", "", ":auth delegated", "second", "", ":q");

        await console.RunAsync(CancellationToken.None);

        Assert.Equal(2, requests.Count);
        Assert.Null(requests[0].Message.TaskId);
        Assert.Null(requests[1].Message.TaskId);
        Assert.Null(requests[1].Message.ContextId);
        Assert.Contains("Cleared the continuing task", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_SameAuthMode_KeepsContinuationTask()
    {
        var requests = new List<SendMessageRequest>();
        var client = new Mock<IA2AClient>(MockBehavior.Strict);
        client.Setup(value => value.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SendMessageRequest, CancellationToken>((request, _) => requests.Add(request))
            .ReturnsAsync(CreateResponse(TaskState.InputRequired));

        var output = new StringWriter();
        A2AConsole console = CreateConsole(
            client.Object,
            new A2AAuthenticationSession { Mode = A2AAuthMode.Delegated },
            output,
            "first", "", ":auth delegated", "second", "", ":q");

        await console.RunAsync(CancellationToken.None);

        Assert.Equal(2, requests.Count);
        Assert.Equal("task-1", requests[1].Message.TaskId);
        Assert.Equal("context-1", requests[1].Message.ContextId);
        Assert.DoesNotContain("Cleared the continuing task", output.ToString(), StringComparison.Ordinal);
    }

    private static SendMessageResponse CreateResponse(TaskState state) => new()
    {
        Task = new AgentTask
        {
            Id = "task-1",
            ContextId = "context-1",
            Status = new global::A2A.TaskStatus
            {
                State = state,
                Message = new Message
                {
                    MessageId = "message-1",
                    Role = Role.Agent,
                    Parts = [Part.FromText("agent reply")],
                },
            },
        },
    };

    private static A2AConsole CreateConsole(
        IA2AClient client,
        A2AAuthenticationSession session,
        TextWriter output,
        params string[] input)
        => CreateConsole(client, session, output, showHistory: false, input);

    private static A2AConsole CreateConsole(
        IA2AClient client,
        A2AAuthenticationSession session,
        TextWriter output,
        bool showHistory,
        params string[] input)
        => new(
            client,
            new AgentCard { Capabilities = new AgentCapabilities { Streaming = false } },
            session,
            new StringReader(string.Join(Environment.NewLine, input) + Environment.NewLine),
            output,
            showHistory,
            usePushNotifications: false,
            new Uri("http://localhost:5000"));
}
