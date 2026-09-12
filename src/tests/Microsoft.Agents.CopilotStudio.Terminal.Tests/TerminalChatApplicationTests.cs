#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Terminal.Gui.App;
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

    private static TerminalPresenter CreatePresenter(ITerminalView view)
    {
        ActivityJournal journal = new();
        ActivityInterpreter interpreter = new();
        ConversationSession session = new(new FakeCopilotConversationClient(), journal, interpreter);
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
