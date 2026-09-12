#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    public void CreateWindow_TabsLayoutBuildsThreeTabsAndSelectsChat()
    {
        using IApplication application = Application.Create();
        using CancellationTokenSource shutdown = new();
        TerminalChatApplication terminal = new(new TerminalOptions(TerminalLayout.Tabs, false));
        TerminalPresenter presenter = CreatePresenter(terminal);

        using Window window = terminal.CreateWindow(application, presenter, shutdown);

        Tabs tabs = Assert.IsType<Tabs>(Assert.Single(window.SubViews, view => view is Tabs));
        Assert.Equal(["_Chat", "_Activities", "_Help"], tabs.TabCollection.Select(view => view.Title));
        Assert.NotNull(tabs.Value);
        Assert.Equal("_Chat", tabs.Value.Title);
        Assert.Contains(window.SubViews, view => view is StatusBar);
        AssertRequiredChatControls(window);
    }

    [Fact]
    public void CreateWindow_SplitLayoutBuildsChatAndActivityFramesWithoutTabs()
    {
        using IApplication application = Application.Create();
        using CancellationTokenSource shutdown = new();
        TerminalChatApplication terminal = new(new TerminalOptions(TerminalLayout.Split, false));
        TerminalPresenter presenter = CreatePresenter(terminal);

        using Window window = terminal.CreateWindow(application, presenter, shutdown);

        Assert.DoesNotContain(window.SubViews, view => view is Tabs);
        Assert.Contains(window.SubViews, view => view is FrameView frame && frame.Title == "_Chat");
        Assert.Contains(window.SubViews, view => view is FrameView frame && frame.Title == "_Activities");
        Assert.Contains(window.SubViews, view => view is StatusBar);
        AssertRequiredChatControls(window);
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
        Assert.Single(descendants.OfType<Markdown>());
        Assert.Single(descendants.OfType<TextField>());
        Assert.Single(descendants.OfType<ListView<ActivityRecord>>());

#pragma warning disable CS0618 // Task 7 requires Terminal.Gui's TextView for the JSON inspector.
        TextView json = Assert.Single(descendants.OfType<TextView>());
#pragma warning restore CS0618
        Assert.True(json.ReadOnly);
        Assert.True(json.ScrollBars);
        Assert.False(descendants.OfType<TextField>().Single().Enabled);
        Assert.Contains(descendants.OfType<Label>(), label => label.Text == "Ready.");
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
}
