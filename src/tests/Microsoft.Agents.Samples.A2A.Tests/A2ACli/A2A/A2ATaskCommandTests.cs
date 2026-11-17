// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Samples.A2ACli.A2A;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2ACli.Tests.A2A;

public class A2ATaskCommandTests
{
    [Fact]
    public async Task Status_RetrievesAndDisplaysTheExistingTaskWithoutSendingAnotherMessage()
    {
        var client = new Mock<IA2AClient>(MockBehavior.Strict);
        client.Setup(value => value.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendMessageResponse { Task = TaskResult(TaskState.InputRequired, "Review the briefing.") });
        client.Setup(value => value.GetTaskAsync(It.Is<GetTaskRequest>(request => request.Id == "outer-task"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TaskResult(TaskState.InputRequired, "Still awaiting approval."));
        var output = new StringWriter();
        var console = Console(client.Object, output, false, "start", "", ":status", ":q");

        await console.RunAsync(CancellationToken.None);

        Assert.Contains("outer-task", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("InputRequired", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Still awaiting approval.", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancel_CancelsTheCurrentTaskAndClearsMessageContinuation()
    {
        var sends = new List<SendMessageRequest>();
        var client = new Mock<IA2AClient>(MockBehavior.Strict);
        client.Setup(value => value.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SendMessageRequest, CancellationToken>((request, ct) => sends.Add(request))
            .ReturnsAsync(new SendMessageResponse { Task = TaskResult(TaskState.InputRequired, "Review.") });
        client.Setup(value => value.CancelTaskAsync(It.Is<CancelTaskRequest>(request => request.Id == "outer-task"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TaskResult(TaskState.Canceled, "Cancelled."));
        var output = new StringWriter();
        var console = Console(client.Object, output, false, "start", "", ":cancel", "another task", "", ":q");

        await console.RunAsync(CancellationToken.None);

        Assert.Contains("Canceled", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(2, sends.Count);
        Assert.Null(sends[1].Message.TaskId);
        Assert.Null(sends[1].Message.ContextId);
    }

    [Fact]
    public async Task Resume_AfterAnInterruptedStream_SubscribesWithoutResubmittingTheWork()
    {
        var client = new Mock<IA2AClient>(MockBehavior.Strict);
        client.Setup(value => value.SendStreamingMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Interrupted());
        client.Setup(value => value.SubscribeToTaskAsync(
                It.Is<SubscribeToTaskRequest>(request => request.Id == "outer-task"), It.IsAny<CancellationToken>()))
            .Returns(Completed());
        var output = new StringWriter();
        var console = Console(client.Object, output, true, "y", "start", "", ":resume", ":q");

        await console.RunAsync(CancellationToken.None);

        Assert.Contains("Request failed", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Recovered final result.", output.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(TaskState.InputRequired)]
    [InlineData(TaskState.AuthRequired)]
    public async Task Resume_ReleasesTheConsoleWhenTheTaskNeedsCallerInput(TaskState suspendedState)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var sends = new List<SendMessageRequest>();
        var client = new Mock<IA2AClient>(MockBehavior.Strict);
        client.Setup(value => value.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Returns<SendMessageRequest, CancellationToken>((request, ct) =>
            {
                sends.Add(request);
                return Task.FromResult(new SendMessageResponse
                {
                    Task = TaskResult(sends.Count == 1 ? TaskState.Working : TaskState.Completed, "Task response."),
                });
            });
        client.Setup(value => value.SubscribeToTaskAsync(It.IsAny<SubscribeToTaskRequest>(), It.IsAny<CancellationToken>()))
            .Returns<SubscribeToTaskRequest, CancellationToken>((request, ct) => Suspended(suspendedState, ct));
        var output = new StringWriter();
        var input = suspendedState == TaskState.InputRequired
            ? new[] { "n", "start", "", ":resume", "the answer", "", ":q" }
            : ["n", "start", "", ":resume", ":q"];
        var console = Console(client.Object, output, true, input);

        await console.RunAsync(timeout.Token);

        Assert.False(console.IsRunning);
        Assert.Contains("Which meeting?", output.ToString(), StringComparison.Ordinal);
        if (suspendedState == TaskState.InputRequired)
        {
            Assert.Equal(2, sends.Count);
            Assert.Equal("outer-task", sends[1].Message.TaskId);
            Assert.Equal("outer-context", sends[1].Message.ContextId);
        }
    }

    [Fact]
    public async Task ChangingAuthentication_DropsTaskReferencesForStatusCommandsToo()
    {
        var client = new Mock<IA2AClient>(MockBehavior.Strict);
        client.Setup(value => value.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendMessageResponse { Task = TaskResult(TaskState.InputRequired, "Review.") });
        var output = new StringWriter();
        var console = Console(client.Object, output, false, "start", "", ":auth delegated", ":status", ":q");

        await console.RunAsync(CancellationToken.None);

        Assert.Contains("No task", output.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(":status")]
    [InlineData(":cancel")]
    [InlineData(":resume")]
    public async Task TaskCommandBeforeAnyRequest_ExplainsThatNoTaskIsAvailable(string command)
    {
        var output = new StringWriter();
        var console = Console(new Mock<IA2AClient>(MockBehavior.Strict).Object, output, false, command, ":q");

        await console.RunAsync(CancellationToken.None);

        Assert.Contains("No task", output.ToString(), StringComparison.Ordinal);
    }

    private static A2AConsole Console(IA2AClient client, TextWriter output, bool streaming, params string[] input)
        => new(client, new AgentCard { Capabilities = new AgentCapabilities { Streaming = streaming } },
            new A2AAuthenticationSession(), new StringReader(string.Join(Environment.NewLine, input) + Environment.NewLine),
            output, showHistory: false, pushNotificationReceiver: null);

    private static AgentTask TaskResult(TaskState state, string text) => new()
    {
        Id = "outer-task",
        ContextId = "outer-context",
        Status = new global::A2A.TaskStatus
        {
            State = state,
            Message = new Message { Role = Role.Agent, Parts = [Part.FromText(text)] },
        },
    };

    private static async IAsyncEnumerable<StreamResponse> Interrupted()
    {
        yield return new StreamResponse { Task = TaskResult(TaskState.Working, "Working.") };
        await Task.CompletedTask;
        throw new HttpRequestException("Stream interrupted.");
    }

    private static async IAsyncEnumerable<StreamResponse> Completed()
    {
        yield return new StreamResponse { Task = TaskResult(TaskState.Completed, "Recovered final result.") };
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<StreamResponse> Suspended(
        TaskState state, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return new StreamResponse { Task = TaskResult(TaskState.Working, "Reattached.") };
        yield return new StreamResponse
        {
            StatusUpdate = new TaskStatusUpdateEvent
            {
                TaskId = "outer-task",
                ContextId = "outer-context",
                Status = new global::A2A.TaskStatus
                {
                    State = state,
                    Message = new Message { Role = Role.Agent, Parts = [Part.FromText("Which meeting?")] },
                },
            },
        };
        // Native subscriptions remain open while a task is suspended, unlike a send response.
        await new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task.WaitAsync(cancellationToken);
    }
}
