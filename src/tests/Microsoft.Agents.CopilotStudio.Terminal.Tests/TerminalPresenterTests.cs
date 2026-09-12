#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;

public sealed class TerminalPresenterTests
{
    [Fact]
    public async Task StartAsync_PublishesRecordBeforeChatChanges()
    {
        FakeCopilotConversationClient client = new()
        {
            StartActivities =
            [
                new Activity
                {
                    Type = ActivityTypes.Message,
                    Text = "Hello"
                }
            ]
        };

        ActivityJournal journal = new();
        ActivityInterpreter interpreter = new();
        FakeTerminalView view = new();
        TerminalPresenter presenter = CreatePresenter(client, journal, interpreter, view);

        await presenter.StartAsync(CancellationToken.None);

        Assert.Equal(["AddActivity:1", "ApplyChatChanges"], view.CallOrder);
        Assert.Single(view.Activities);
        Assert.Single(view.ChatChanges);
        Assert.Equal("Hello", view.Activities[0].Summary);
    }

    [Fact]
    public async Task SendAsync_TogglesBusyState()
    {
        TaskCompletionSource<object?> executeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<object?> releaseExecute = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeCopilotConversationClient client = new()
        {
            ExecuteStarted = executeStarted,
            ReleaseExecute = releaseExecute
        };

        ActivityJournal journal = new();
        ActivityInterpreter interpreter = new();
        FakeTerminalView view = new();
        TerminalPresenter presenter = CreatePresenter(client, journal, interpreter, view);

        Task send = presenter.SendAsync("Hello", CancellationToken.None);
        await executeStarted.Task;

        Assert.Equal([true], view.BusyStates);

        releaseExecute.SetResult(null);
        await send;

        Assert.Equal([true, false], view.BusyStates);
        Assert.Contains(view.CallOrder, call => call == "SetBusy:True");
        Assert.Contains(view.CallOrder, call => call == "SetBusy:False");
    }

    [Fact]
    public async Task SendAsync_TransportFailure_AppendsSingleDiagnosticAndSetsErrorStatus()
    {
        InvalidOperationException failure = new("boom");
        FakeCopilotConversationClient client = new()
        {
            ExecuteException = failure
        };

        ActivityJournal journal = new();
        ActivityInterpreter interpreter = new();
        FakeTerminalView view = new();
        TerminalPresenter presenter = CreatePresenter(client, journal, interpreter, view);

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => presenter.SendAsync("Hello", CancellationToken.None));

        Assert.Same(failure, thrown);
        Assert.Single(view.Statuses, status => status.Severity == DiagnosticSeverity.Error);
        Assert.Contains(view.Statuses, status => status.Text.Contains("boom", StringComparison.OrdinalIgnoreCase));
        Assert.Single(journal.Snapshot(), record => record.Direction == ActivityDirection.Diagnostic);
        Assert.Single(view.Activities, record => record.Direction == ActivityDirection.Diagnostic);
    }

    [Fact]
    public async Task SendAsync_WhitespaceText_ShowsInformationStatusWithoutInvokingSession()
    {
        FakeCopilotConversationClient client = new();
        ActivityJournal journal = new();
        ActivityInterpreter interpreter = new();
        FakeTerminalView view = new();
        TerminalPresenter presenter = CreatePresenter(client, journal, interpreter, view);

        await presenter.SendAsync("   ", CancellationToken.None);

        Assert.Empty(client.Requests);
        Assert.Empty(view.Activities);
        Assert.Empty(view.ChatChanges);
        Assert.Empty(view.BusyStates);
        Assert.Single(view.Statuses, status => status.Severity == DiagnosticSeverity.Information);
        Assert.Contains(view.Statuses, status => status.Text.Contains("message", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Dispose_DetachesViewFromSessionEvents()
    {
        FakeCopilotConversationClient client = new()
        {
            StartActivities =
            [
                new Activity
                {
                    Type = ActivityTypes.Message,
                    Text = "Late activity"
                }
            ]
        };
        ActivityJournal journal = new();
        ActivityInterpreter interpreter = new();
        FakeTerminalView view = new();
        TerminalPresenter presenter = CreatePresenter(client, journal, interpreter, view);

        presenter.Dispose();
        await presenter.StartAsync(CancellationToken.None);

        Assert.Empty(view.Activities);
        Assert.Empty(view.ChatChanges);
    }

    private static TerminalPresenter CreatePresenter(
        FakeCopilotConversationClient client,
        ActivityJournal journal,
        ActivityInterpreter interpreter,
        FakeTerminalView view)
    {
        ConversationSession session = new(client, journal, interpreter);
        return new TerminalPresenter(session, journal, interpreter, view);
    }
}
