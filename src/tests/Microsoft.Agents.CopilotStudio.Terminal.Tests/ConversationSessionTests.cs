#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;

public sealed class ConversationSessionTests
{
    [Fact]
    public async Task StartAsync_PublishesStartActivitiesAsInbound()
    {
        FakeCopilotConversationClient client = new()
        {
            StartActivities =
            [
                new Activity { Type = ActivityTypes.Typing, Text = "Working..." },
                new Activity { Type = ActivityTypes.Message, Text = "Hello" }
            ]
        };
        ConversationSession session = CreateSession(client);
        List<(ActivityDirection Direction, string? Text)> published = [];
        session.ActivityPublished += (_, eventArgs) => published.Add((eventArgs.Direction, eventArgs.Activity.Text));

        await session.StartAsync(CancellationToken.None);

        Assert.Equal(
            [
                (ActivityDirection.Inbound, "Working..."),
                (ActivityDirection.Inbound, "Hello")
            ],
            published);
    }

    [Fact]
    public async Task SendAsync_PublishesOutboundBeforeInboundResponses()
    {
        FakeCopilotConversationClient client = new()
        {
            ExecuteActivities =
            [
                new Activity { Type = ActivityTypes.Typing, Text = "Working..." },
                new Activity { Type = ActivityTypes.Message, Text = "Hello back" }
            ]
        };
        ConversationSession session = CreateSession(client);
        List<(ActivityDirection Direction, string? Text)> published = [];
        List<bool> busyChanges = [];
        session.ActivityPublished += (_, eventArgs) => published.Add((eventArgs.Direction, eventArgs.Activity.Text));
        session.BusyChanged += (_, busy) => busyChanges.Add(busy);

        await session.SendAsync("Hello", CancellationToken.None);

        Activity request = Assert.Single(client.Requests);
        Assert.Equal(ActivityTypes.Message, request.Type);
        Assert.Equal("Hello", request.Text);
        Assert.Equal(session.ConversationId, request.Conversation.Id);
        Assert.Equal(
            [
                (ActivityDirection.Outbound, "Hello"),
                (ActivityDirection.Inbound, "Working..."),
                (ActivityDirection.Inbound, "Hello back")
            ],
            published);
        Assert.Equal([true, false], busyChanges);
    }

    [Fact]
    public async Task SendAsync_ConcurrentSend_ThrowsInvalidOperationException()
    {
        TaskCompletionSource<object?> executeStarted = NewSignal();
        TaskCompletionSource<object?> releaseExecute = NewSignal();
        FakeCopilotConversationClient client = new()
        {
            ExecuteStarted = executeStarted,
            ReleaseExecute = releaseExecute
        };
        ConversationSession session = CreateSession(client);

        Task firstSend = session.SendAsync("first", CancellationToken.None);
        await executeStarted.Task;

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.SendAsync("second", CancellationToken.None));

        Assert.Contains("busy", exception.Message, StringComparison.OrdinalIgnoreCase);

        releaseExecute.SetResult(null);
        await firstSend;
    }

    [Fact]
    public async Task SendAsync_Cancellation_DoesNotRaiseFailed()
    {
        TaskCompletionSource<object?> executeStarted = NewSignal();
        TaskCompletionSource<object?> releaseExecute = NewSignal();
        FakeCopilotConversationClient client = new()
        {
            ExecuteStarted = executeStarted,
            ReleaseExecute = releaseExecute
        };
        ConversationSession session = CreateSession(client);
        List<Exception> failures = [];
        List<bool> busyChanges = [];
        session.Failed += (_, exception) => failures.Add(exception);
        session.BusyChanged += (_, busy) => busyChanges.Add(busy);

        using CancellationTokenSource cancellationSource = new();
        Task send = session.SendAsync("hello", cancellationSource.Token);
        await executeStarted.Task;

        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => send);

        client.ReleaseExecute = null;
        await session.SendAsync("after cancel", CancellationToken.None);

        Assert.Empty(failures);
        Assert.Equal([true, false, true, false], busyChanges);
    }

    [Fact]
    public async Task SendAsync_TransportFailure_RaisesFailedAndResetsBusy()
    {
        InvalidOperationException failure = new("boom");
        FakeCopilotConversationClient client = new()
        {
            ExecuteException = failure
        };
        ConversationSession session = CreateSession(client);
        List<Exception> failures = [];
        List<bool> busyChanges = [];
        session.Failed += (_, exception) => failures.Add(exception);
        session.BusyChanged += (_, busy) => busyChanges.Add(busy);

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.SendAsync("hello", CancellationToken.None));

        Assert.Same(failure, thrown);
        Assert.Same(failure, Assert.Single(failures));
        Assert.Equal([true, false], busyChanges);

        client.ExecuteException = null;
        await session.SendAsync("retry", CancellationToken.None);
        Assert.Equal(2, client.Requests.Count);
    }

    [Fact]
    public async Task SendAsync_WhitespaceText_ThrowsArgumentException()
    {
        ConversationSession session = CreateSession(new FakeCopilotConversationClient());

        await Assert.ThrowsAsync<ArgumentException>(() => session.SendAsync("   ", CancellationToken.None));
    }

    [Fact]
    public void ConversationId_ExposesGeneratedSessionIdentifier()
    {
        ConversationSession session = CreateSession(new FakeCopilotConversationClient());

        Assert.Matches("^Sample-[0-9a-f]{32}$", session.ConversationId);
    }

    private static ConversationSession CreateSession(ICopilotConversationClient client)
    {
        return new ConversationSession(client, new ActivityJournal(), new ActivityInterpreter());
    }

    private static TaskCompletionSource<object?> NewSignal()
    {
        return new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
