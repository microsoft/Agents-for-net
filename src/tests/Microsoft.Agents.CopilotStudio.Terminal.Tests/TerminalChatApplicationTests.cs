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

[Collection("TerminalGui")]
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
    public void CreateWindow_DefaultLayoutUsesChromeFreeTimelineAndHiddenInspectors()
    {
        using IApplication application = Application.Create();
        using CancellationTokenSource shutdown = new();
        TerminalChatApplication terminal = new(TerminalOptions.Parse([]));
        using TerminalPresenter presenter = CreatePresenter(terminal);

        using Window window = terminal.CreateWindow(application, presenter, shutdown);

        Assert.DoesNotContain(Descendants(window), view => view is Tabs);
        View surfaces = Assert.Single(window.SubViews);
        Assert.IsNotType<Tabs>(surfaces);
        Assert.Equal(
            ["_Chat", "_Thoughts", "_Activities", "_Help"],
            surfaces.SubViews.Select(view => view.Title));
        AssertDefaultSurface(surfaces, "_Chat");
        AssertRequiredChatControls(window);
    }

    [Fact]
    public async Task DefaultLayout_ShortcutsSwitchVisibleSurfaceAndFocusItsPrimaryControl()
    {
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        using CancellationTokenSource shutdown = new();
        TerminalChatApplication terminal = new(TerminalOptions.Parse([]));
        using TerminalPresenter presenter = CreatePresenter(terminal);
        using Window window = terminal.CreateWindow(application, presenter, shutdown);
        View surfaces = Assert.Single(window.SubViews);
        View thoughts = Assert.Single(surfaces.SubViews, view => view.Title == "_Thoughts");
        View activities = Assert.Single(surfaces.SubViews, view => view.Title == "_Activities");
        View help = Assert.Single(surfaces.SubViews, view => view.Title == "_Help");
        TerminalTimelineView thoughtTimeline = Assert.Single(
            Descendants(thoughts).OfType<TerminalTimelineView>());
        ListView<ActivityRecord> activityList = Assert.Single(
            Descendants(activities).OfType<ListView<ActivityRecord>>());
        TextField composer = Assert.Single(Descendants(surfaces).OfType<TextField>());
        List<(string Title, bool PrimaryControlFocused)> observed = [];

        await terminal.MonitorStartupAsync(
            Task.CompletedTask,
            action => action(),
            () => throw new InvalidOperationException("Successful startup must not stop the application."),
            CancellationToken.None);
        application.Iteration += (_, _) =>
        {
            window.NewKeyDownEvent(Key.D2.WithCtrl);
            observed.Add((Assert.Single(surfaces.SubViews, view => view.Visible).Title, thoughtTimeline.HasFocus));

            window.NewKeyDownEvent(Key.D3.WithCtrl);
            observed.Add((Assert.Single(surfaces.SubViews, view => view.Visible).Title, activityList.HasFocus));

            window.NewKeyDownEvent(Key.D4.WithCtrl);
            observed.Add((Assert.Single(surfaces.SubViews, view => view.Visible).Title, help.HasFocus));

            window.NewKeyDownEvent(Key.D1.WithCtrl);
            observed.Add((Assert.Single(surfaces.SubViews, view => view.Visible).Title, composer.HasFocus));
            application.RequestStop();
        };

        application.Run(window);

        Assert.Equal(["_Thoughts", "_Activities", "_Help", "_Chat"], observed.Select(item => item.Title));
        Assert.All(observed, item => Assert.True(item.PrimaryControlFocused, $"{item.Title} did not receive focus."));
    }

    [Fact]
    public void CreateWindow_TabsOptionUsesChromeFreeTimelineMode()
    {
        using IApplication application = Application.Create();
        using CancellationTokenSource shutdown = new();
        TerminalChatApplication terminal = new(TerminalOptions.Parse(["--layout", "tabs"]));
        using TerminalPresenter presenter = CreatePresenter(terminal);

        using Window window = terminal.CreateWindow(application, presenter, shutdown);

        Assert.DoesNotContain(Descendants(window), view => view is Tabs);
        View surfaces = Assert.Single(window.SubViews);
        AssertDefaultSurface(surfaces, "_Chat");
        AssertRequiredChatControls(window);
    }

    [Fact]
    public void CreateWindow_DefaultLayoutUsesTimelineConversationSurfaceAndBorderedComposer()
    {
        using IApplication application = Application.Create();
        using CancellationTokenSource shutdown = new();
        TerminalChatApplication terminal = new(TerminalOptions.Parse([]));
        using TerminalPresenter presenter = CreatePresenter(terminal);

        using Window window = terminal.CreateWindow(application, presenter, shutdown);

        View surfaces = Assert.Single(window.SubViews);
        View chat = Assert.Single(surfaces.SubViews, view => view.Title == "_Chat");
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
        View surfaces = Assert.Single(window.SubViews);
        View chat = Assert.Single(surfaces.SubViews, view => view.Title == "_Chat");
        View thoughts = Assert.Single(surfaces.SubViews, view => view.Title == "_Thoughts");

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
            line => PlainText(line) == "Reasoning complete · F2 for details");
        Assert.Contains(thoughtInspector.RenderedLines, line => PlainText(line) == "Checking account");
        Assert.Contains(thoughtInspector.RenderedLines, line => PlainText(line) == "Compared all records");
    }

    [Fact]
    public void CreateWindow_DefaultLayoutKeepsTranscriptVisibleWithoutOverlappingBottomStackAtShortHeights()
    {
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        using CancellationTokenSource shutdown = new();
        TerminalChatApplication terminal = new(TerminalOptions.Parse([]));
        using TerminalPresenter presenter = CreatePresenter(terminal);
        using Window window = terminal.CreateWindow(application, presenter, shutdown);

        View surfaces = Assert.Single(window.SubViews);
        View chat = Assert.Single(surfaces.SubViews, view => view.Title == "_Chat");
        View conversation = Assert.Single(chat.SubViews);

        conversation.Frame = new System.Drawing.Rectangle(0, 0, 50, 7);
        Assert.True(conversation.Layout());

        TimelineRoleLabel header = Assert.Single(conversation.SubViews.OfType<TimelineRoleLabel>());
        TerminalTimelineView transcript = Assert.Single(conversation.SubViews.OfType<TerminalTimelineView>());
        Label status = Assert.Single(conversation.SubViews.OfType<Label>(), label => label.Text == "Ready.");
        FrameView composer = Assert.Single(conversation.SubViews.OfType<FrameView>(), frame => frame.Title == "_Message");
        StatusBar footer = Assert.Single(conversation.SubViews.OfType<StatusBar>());
        View actionBar = Assert.Single(
            conversation.SubViews,
            view => view is not TimelineRoleLabel
                && view is not TerminalTimelineView
                && view is not Label
                && view is not FrameView
                && view is not StatusBar);

        Assert.True(transcript.Frame.Height >= 1, $"Transcript frame: {transcript.Frame}");
        Assert.True(
            header.Frame.Y + header.Frame.Height <= transcript.Frame.Y,
            $"Header {header.Frame} overlaps transcript {transcript.Frame}");
        Assert.True(
            transcript.Frame.Y + transcript.Frame.Height <= actionBar.Frame.Y,
            $"Transcript {transcript.Frame} overlaps action bar {actionBar.Frame}");
        Assert.True(
            actionBar.Frame.Y + actionBar.Frame.Height <= status.Frame.Y,
            $"Action bar {actionBar.Frame} overlaps status {status.Frame}");
        Assert.True(
            status.Frame.Y + status.Frame.Height <= composer.Frame.Y,
            $"Status {status.Frame} overlaps composer {composer.Frame}");
        Assert.True(
            composer.Frame.Y + composer.Frame.Height <= footer.Frame.Y,
            $"Composer {composer.Frame} overlaps footer {footer.Frame}");
        Assert.True(
            footer.Frame.Y + footer.Frame.Height <= conversation.Frame.Height,
            $"Footer {footer.Frame} exceeds conversation height {conversation.Frame.Height}");
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

    private static void AssertDefaultSurface(View surfaces, string visibleTitle)
    {
        Assert.Collection(
            surfaces.SubViews,
            view => Assert.Equal(view.Title == visibleTitle, view.Visible),
            view => Assert.Equal(view.Title == visibleTitle, view.Visible),
            view => Assert.Equal(view.Title == visibleTitle, view.Visible),
            view => Assert.Equal(view.Title == visibleTitle, view.Visible));
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
