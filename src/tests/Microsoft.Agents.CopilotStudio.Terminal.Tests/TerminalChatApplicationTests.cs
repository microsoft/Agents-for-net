#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

public sealed class TerminalChatApplicationTests
{
    [Theory]
    [InlineData("https://example.com", true)]
    [InlineData("http://example.com", true)]
    [InlineData("file:///C:/Windows/System32/calc.exe", false)]
    [InlineData("mailto:test@example.com", false)]
    public void CanOpenLink_AllowsOnlyHttpAndHttps(string url, bool expected)
    {
        Assert.Equal(expected, TerminalChatApplication.CanOpenLink(new Uri(url)));
    }

    [Theory]
    [InlineData(null, "Use default", false)]
    [InlineData("", "Use default", false)]
    [InlineData("send this", "send this", true)]
    public void ResolveAction_PopulatesComposerAndSendsOnlyNonemptyValues(
        string? value,
        string expectedText,
        bool expectedSend)
    {
        (string text, bool send) =
            TerminalChatApplication.ResolveAction(new ChatAction("Use default", value));

        Assert.Equal(expectedText, text);
        Assert.Equal(expectedSend, send);
    }

    [Fact]
    public void ReceivedLink_KeepsReceivedUriOutOfTerminalGuiUrl()
    {
        Uri target = new("https://example.com/sensitive");
        using ReceivedLink link = new("Example", target, _ => { });

        Assert.Same(target, link.Target);
        Assert.Equal(string.Empty, link.Url);

        link.Url = target.AbsoluteUri;

        Assert.Equal(string.Empty, link.Url);
    }

    [Theory]
    [InlineData(Command.Accept)]
    [InlineData(Command.Activate)]
    public void ReceivedLink_AcceptAndActivateCommandsRequestConfirmedOpen(Command command)
    {
        Uri target = new("https://example.com/");
        Uri? requestedTarget = null;
        using ReceivedLink link = new("Example", target, uri => requestedTarget = uri);

        link.InvokeCommand(command);

        Assert.Same(target, requestedTarget);
        Assert.Equal(string.Empty, link.Url);
    }

    [Fact]
    public void OpenReceivedLink_DeclinedConfirmationDoesNotStartProcess()
    {
        int starts = 0;
        TerminalChatApplication terminal = new(
            new TerminalOptions(TerminalLayout.Tabs, false),
            _ => false,
            _ => starts++);

        terminal.OpenReceivedLink(new Uri("https://example.com/"));

        Assert.Equal(0, starts);
    }

    [Fact]
    public void OpenReceivedLink_AcceptedConfirmationStartsExactUriWithShellExecution()
    {
        Uri target = new("https://example.com/path?q=value");
        System.Diagnostics.ProcessStartInfo? startInfo = null;
        TerminalChatApplication terminal = new(
            new TerminalOptions(TerminalLayout.Tabs, false),
            _ => true,
            value => startInfo = value);

        terminal.OpenReceivedLink(target);

        Assert.NotNull(startInfo);
        Assert.Equal(target.AbsoluteUri, startInfo.FileName);
        Assert.True(startInfo.UseShellExecute);
    }

    [Fact]
    public void CreateWindow_TabsLayoutBuildsFourTabsAndSelectsChat()
    {
        using IApplication application = Application.Create();
        using CancellationTokenSource shutdown = new();
        TerminalChatApplication terminal = new(new TerminalOptions(TerminalLayout.Tabs, false));
        TerminalPresenter presenter = CreatePresenter(terminal);

        using Window window = terminal.CreateWindow(application, presenter, shutdown);

        Tabs tabs = Assert.IsType<Tabs>(Assert.Single(window.SubViews, view => view is Tabs));
        Assert.Equal(["_Chat", "_Thoughts", "_Activities", "_Help"], tabs.TabCollection.Select(view => view.Title));
        Assert.NotNull(tabs.Value);
        Assert.Equal("_Chat", tabs.Value.Title);
        AssertRequiredChatControls(window);
    }

    [Fact]
    public void CreateWindow_TabsLayoutUsesTimelineConversationSurfaceAndBorderedComposer()
    {
        using IApplication application = Application.Create();
        using CancellationTokenSource shutdown = new();
        TerminalChatApplication terminal = new(new TerminalOptions(TerminalLayout.Tabs, false));
        using TerminalPresenter presenter = CreatePresenter(terminal);

        using Window window = terminal.CreateWindow(application, presenter, shutdown);

        Tabs tabs = Assert.IsType<Tabs>(Assert.Single(window.SubViews, view => view is Tabs));
        View chat = Assert.Single(tabs.TabCollection, view => view.Title == "_Chat");
        TerminalTimelineView timeline = Assert.Single(
            Descendants(chat).OfType<TerminalTimelineView>(),
            view => view.CollapseCompletedThoughts);

        Assert.DoesNotContain(Descendants(chat), view => view is FrameView { Title: "_Conversation" });
        Assert.Contains(Descendants(chat).OfType<FrameView>(), frame => frame.Title == "_Message");
        Assert.Equal(TimelineGlyphSet.ForEncoding(Console.OutputEncoding), timeline.Glyphs);
    }

    [Fact]
    public void CreateWindow_SplitLayoutBuildsChatAndThoughtTabsBesideActivities()
    {
        using IApplication application = Application.Create();
        using CancellationTokenSource shutdown = new();
        TerminalChatApplication terminal = new(new TerminalOptions(TerminalLayout.Split, false));
        TerminalPresenter presenter = CreatePresenter(terminal);

        using Window window = terminal.CreateWindow(application, presenter, shutdown);

        Tabs tabs = Assert.IsType<Tabs>(Assert.Single(window.SubViews, view => view is Tabs));
        Assert.Equal(["_Chat", "_Thoughts"], tabs.TabCollection.Select(view => view.Title));
        Assert.Contains(window.SubViews, view => view.Title == "_Activities");
        Assert.DoesNotContain(window.SubViews, view => view is FrameView { Title: "_Activities" });
        AssertRequiredChatControls(window);
    }

    [Fact]
    public void ApplyChatChanges_ShowsActiveThoughtInlineAndFullThoughtsInInspector()
    {
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        using CancellationTokenSource shutdown = new();
        TerminalChatApplication terminal = new(new TerminalOptions(TerminalLayout.Tabs, false));
        using TerminalPresenter presenter = CreatePresenter(terminal);
        using Window window = terminal.CreateWindow(application, presenter, shutdown);
        Tabs tabs = Assert.IsType<Tabs>(Assert.Single(window.SubViews, view => view is Tabs));
        View chat = Assert.Single(tabs.TabCollection, view => view.Title == "_Chat");
        View thoughts = Assert.Single(tabs.TabCollection, view => view.Title == "_Thoughts");

        terminal.ApplyChatChanges(
        [
            new ChatChange(
                ChatChangeKind.Upsert,
                "thought-active",
                new ChatEntry(
                    "thought-active",
                    ChatEntryKind.Thought,
                    "Reasoning",
                    "Checking account",
                    true,
                    [],
                    [],
                    "thought-active")),
            new ChatChange(
                ChatChangeKind.Upsert,
                "thought-complete",
                new ChatEntry(
                    "thought-complete",
                    ChatEntryKind.Thought,
                    "Reasoning",
                    "Compared all records",
                    false,
                    [],
                    [],
                    "thought-complete"))
        ]);
        RunOneIteration(application, window);

        TerminalTimelineView conversation = Assert.Single(
            Descendants(chat).OfType<TerminalTimelineView>(),
            view => view.CollapseCompletedThoughts);
        TerminalTimelineView thoughtInspector = Assert.Single(
            Descendants(thoughts).OfType<TerminalTimelineView>(),
            view => !view.CollapseCompletedThoughts);

        Assert.Contains(conversation.RenderedLines, line => PlainText(line) == "Checking account");
        Assert.DoesNotContain(conversation.RenderedLines, line => PlainText(line) == "Compared all records");
        Assert.Contains(
            conversation.RenderedLines,
            line => PlainText(line) == "Reasoning complete · Ctrl+2 for details");
        Assert.Contains(thoughtInspector.RenderedLines, line => PlainText(line) == "Checking account");
        Assert.Contains(thoughtInspector.RenderedLines, line => PlainText(line) == "Compared all records");
    }

    [Fact]
    public async Task Composer_CannotSendUntilStartupSucceeds()
    {
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        using CancellationTokenSource shutdown = new();
        FakeCopilotConversationClient client = new()
        {
            ExecuteStarted = new TaskCompletionSource<object?>(
                TaskCreationOptions.RunContinuationsAsynchronously)
        };
        TerminalChatApplication terminal = new(new TerminalOptions(TerminalLayout.Tabs, false));
        using TerminalPresenter presenter = CreatePresenter(terminal, client);
        using Window window = terminal.CreateWindow(application, presenter, shutdown);
        TextField composer = Assert.Single(Descendants(window).OfType<TextField>());

        composer.Value = "too early";
        terminal.SubmitComposer();

        Assert.False(composer.Enabled);
        Assert.Empty(client.Requests);

        await terminal.MonitorStartupAsync(
            Task.CompletedTask,
            action => action(),
            () => throw new InvalidOperationException("Successful startup must not stop the application."),
            CancellationToken.None);

        Assert.True(composer.Enabled);
        composer.Value = "ready";
        terminal.SubmitComposer();
        await client.ExecuteStarted.Task;

        Assert.Single(client.Requests);
        Assert.Equal("ready", client.Requests[0].Text);
    }

    [Fact]
    public async Task Composer_StartupFailureLeavesComposerDisabled()
    {
        using IApplication application = Application.Create();
        using CancellationTokenSource shutdown = new();
        TerminalChatApplication terminal = new(new TerminalOptions(TerminalLayout.Tabs, false));
        using TerminalPresenter presenter = CreatePresenter(terminal);
        using Window window = terminal.CreateWindow(application, presenter, shutdown);
        TextField composer = Assert.Single(Descendants(window).OfType<TextField>());
        bool stopRequested = false;

        await terminal.MonitorStartupAsync(
            Task.FromException(new InvalidOperationException("startup failed")),
            action => action(),
            () => stopRequested = true,
            CancellationToken.None);

        Assert.False(composer.Enabled);
        Assert.True(stopRequested);
    }

    [Fact]
    public void ApplyChatChanges_OneActivityShowsSuggestedActionAndAllAdaptiveCardLinks()
    {
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        using CancellationTokenSource shutdown = new();
        TerminalChatApplication terminal = new(new TerminalOptions(TerminalLayout.Tabs, false));
        using TerminalPresenter presenter = CreatePresenter(terminal);
        using Window window = terminal.CreateWindow(application, presenter, shutdown);
        Activity activity = new()
        {
            Id = "response-1",
            Type = ActivityTypes.Message,
            Text = "Choose",
            SuggestedActions = new SuggestedActions(actions:
            [
                new CardAction { Title = "Continue", Value = "continue" }
            ]),
            Attachments =
            [
                new Attachment
                {
                    ContentType = ContentTypes.AdaptiveCard,
                    Content = """{"type":"AdaptiveCard","actions":[{"type":"Action.OpenUrl","title":"First","url":"https://first.example"}]}"""
                },
                new Attachment
                {
                    ContentType = ContentTypes.AdaptiveCard,
                    Content = """{"type":"AdaptiveCard","actions":[{"type":"Action.OpenUrl","title":"Second","url":"https://second.example"}]}"""
                }
            ]
        };

        terminal.ApplyChatChanges(new ActivityInterpreter().Process(activity, ActivityDirection.Inbound));
        RunOneIteration(application, window);

        Assert.Equal(
            ["https://first.example/", "https://second.example/"],
            Descendants(window).OfType<ReceivedLink>().Select(link => link.Target.AbsoluteUri));
        Button action = Assert.Single(Descendants(window).OfType<Button>());
        Assert.Equal("Continue", action.Text);
    }

    [Fact]
    public void ApplyChatChanges_NewerActionableMessageReplacesActionBar()
    {
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        using CancellationTokenSource shutdown = new();
        TerminalChatApplication terminal = new(new TerminalOptions(TerminalLayout.Tabs, false));
        using TerminalPresenter presenter = CreatePresenter(terminal);
        using Window window = terminal.CreateWindow(application, presenter, shutdown);
        ActivityInterpreter interpreter = new();

        terminal.ApplyChatChanges(interpreter.Process(
            new Activity
            {
                Id = "first",
                Type = ActivityTypes.Message,
                SuggestedActions = new SuggestedActions(actions:
                [
                    new CardAction { Title = "Old action", Value = "old" }
                ])
            },
            ActivityDirection.Inbound));
        terminal.ApplyChatChanges(interpreter.Process(
            new Activity
            {
                Id = "second",
                Type = ActivityTypes.Message,
                SuggestedActions = new SuggestedActions(actions:
                [
                    new CardAction { Title = "New action", Value = "new" }
                ])
            },
            ActivityDirection.Inbound));
        RunOneIteration(application, window);

        Button action = Assert.Single(Descendants(window).OfType<Button>());
        Assert.Equal("New action", action.Text);
    }

    [Fact]
    public async Task SubmitComposer_ClearsCurrentActionBar()
    {
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        using CancellationTokenSource shutdown = new();
        FakeCopilotConversationClient client = new()
        {
            ExecuteStarted = new TaskCompletionSource<object?>(
                TaskCreationOptions.RunContinuationsAsynchronously)
        };
        TerminalChatApplication terminal = new(new TerminalOptions(TerminalLayout.Tabs, false));
        using TerminalPresenter presenter = CreatePresenter(terminal, client);
        using Window window = terminal.CreateWindow(application, presenter, shutdown);
        TextField composer = Assert.Single(Descendants(window).OfType<TextField>());

        await terminal.MonitorStartupAsync(
            Task.CompletedTask,
            action => action(),
            () => throw new InvalidOperationException("Successful startup must not stop the application."),
            CancellationToken.None);
        terminal.ApplyChatChanges(
        [
            ActionableChange("entry", "Send action", "send")
        ]);
        RunOneIteration(application, window);
        Assert.Single(Descendants(window).OfType<Button>());

        composer.Value = "new request";
        terminal.SubmitComposer();
        await client.ExecuteStarted!.Task;

        Assert.Empty(Descendants(window).OfType<Button>());
    }

    private static TerminalPresenter CreatePresenter(ITerminalView view)
    {
        return CreatePresenter(view, new FakeCopilotConversationClient());
    }

    private static TerminalPresenter CreatePresenter(
        ITerminalView view,
        FakeCopilotConversationClient client)
    {
        ActivityJournal journal = new();
        ActivityInterpreter interpreter = new();
        ConversationSession session = new(client, journal, interpreter);
        return new TerminalPresenter(session, journal, interpreter, view);
    }

    private static void AssertRequiredChatControls(View root)
    {
        IReadOnlyList<View> descendants = Descendants(root).ToArray();
        Assert.Equal(2, descendants.OfType<TerminalTimelineView>().Count());
        Assert.DoesNotContain(descendants, view => view is Markdown);
        Assert.Single(descendants.OfType<TextField>());
        Assert.Single(descendants.OfType<ListView<ActivityRecord>>());
        Assert.Contains(descendants.OfType<FrameView>(), frame => frame.Title == "_Message");
        Assert.Single(descendants.OfType<StatusBar>());

#pragma warning disable CS0618 // Task 7 requires Terminal.Gui's TextView for the JSON inspector.
        TextView json = Assert.Single(descendants.OfType<TextView>());
#pragma warning restore CS0618
        Assert.True(json.ReadOnly);
        Assert.True(json.ScrollBars);
        Assert.False(descendants.OfType<TextField>().Single().Enabled);
        Assert.Contains(descendants.OfType<Label>(), label => label.Text == "Ready.");
    }

    private static ChatChange ActionableChange(string key, string title, string value)
    {
        return new ChatChange(
            ChatChangeKind.Upsert,
            key,
            new ChatEntry(
                key,
                ChatEntryKind.Agent,
                "Agent",
                "Choose",
                false,
                [],
                [new ChatAction(title, value)],
                key));
    }

    private static void RunOneIteration(IApplication application, Window window)
    {
        application.StopAfterFirstIteration = true;
        application.Run(window);
    }

    private static IEnumerable<View> Descendants(View view)
    {
        foreach (View child in view.SubViews)
        {
            yield return child;
            foreach (View descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static string PlainText(TimelineLine line)
    {
        return string.Concat(line.Spans.Select(span => span.Text));
    }
}
