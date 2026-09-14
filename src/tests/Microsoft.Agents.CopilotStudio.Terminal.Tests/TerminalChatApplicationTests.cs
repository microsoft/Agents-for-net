#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.Text;
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
    public void ApplyChatChanges_CreatesControlOnlyForSafeMarkdownLink()
    {
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        using CancellationTokenSource shutdown = new();
        TerminalChatApplication terminal = new(new TerminalOptions(TerminalLayout.Tabs, false));
        using TerminalPresenter presenter = CreatePresenter(terminal);
        using Runnable shell = terminal.CreateShell(application, presenter, shutdown);

        terminal.ApplyChatChanges(
        [
            MarkdownChange("safe", "[Docs](https://example.com/docs)"),
            MarkdownChange("unsafe", "[Local](file:///C:/secret.txt)")
        ]);
        RunOneIteration(application, shell);

        ReceivedLink link = Assert.Single(Descendants(shell).OfType<ReceivedLink>());
        Assert.Equal("Docs", link.Text);
        Assert.Equal("https://example.com/docs", link.Target.AbsoluteUri);
        TerminalTimelineView timeline = Assert.Single(
            Descendants(shell).OfType<TerminalTimelineView>(),
            view => view.CollapseCompletedThoughts);
        Assert.Contains(
            timeline.RenderedLines,
            line => PlainText(line) == "[Local](file:///C:/secret.txt)");
    }

    [Fact]
    public void ApplyChatChanges_StreamReplacementUpdatesMarkdownLinkControlByKey()
    {
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        using CancellationTokenSource shutdown = new();
        TerminalChatApplication terminal = new(new TerminalOptions(TerminalLayout.Tabs, false));
        using TerminalPresenter presenter = CreatePresenter(terminal);
        using Runnable shell = terminal.CreateShell(application, presenter, shutdown);

        terminal.ApplyChatChanges(
        [
            MarkdownChange(
                "stream",
                "[Old](https://example.com/old)",
                isTransient: true)
        ]);
        int stage = 0;
        int attempts = 0;
        application.Iteration += (_, _) =>
        {
            ReceivedLink[] links = Descendants(shell).OfType<ReceivedLink>().ToArray();
            if (links.Length == 0 && ++attempts < 5)
            {
                return;
            }

            try
            {
                ReceivedLink link = Assert.Single(links);
                if (stage == 0)
                {
                    Assert.Equal("https://example.com/old", link.Target.AbsoluteUri);
                    stage = 1;
                    terminal.ApplyChatChanges(
                    [
                        MarkdownChange(
                            "stream",
                            "[Current](https://example.com/current)",
                            isTransient: true)
                    ]);
                    return;
                }

                Assert.Equal("Current", link.Text);
                Assert.Equal("https://example.com/current", link.Target.AbsoluteUri);
                stage = 2;
                application.RequestStop();
            }
            catch
            {
                application.RequestStop();
                throw;
            }
        };

        application.Run(shell);
        Assert.Equal(2, stage);
    }

    [Fact]
    public void MarkdownLink_FocusedActivationRequestsExactUrlThroughConfirmation()
    {
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        using CancellationTokenSource shutdown = new();
        Uri target = new("https://example.com/docs?q=terminal");
        Uri? confirmationTarget = null;
        int starts = 0;
        TerminalChatApplication terminal = new(
            new TerminalOptions(TerminalLayout.Tabs, false),
            uri =>
            {
                confirmationTarget = uri;
                return false;
            },
            _ => starts++);
        using TerminalPresenter presenter = CreatePresenter(terminal);
        using Runnable shell = terminal.CreateShell(application, presenter, shutdown);

        terminal.ApplyChatChanges([MarkdownChange("link", $"[Docs]({target.AbsoluteUri})")]);
        int attempts = 0;
        application.Iteration += (_, _) =>
        {
            ReceivedLink[] links = Descendants(shell).OfType<ReceivedLink>().ToArray();
            if (links.Length == 0 && ++attempts < 5)
            {
                return;
            }

            try
            {
                ReceivedLink link = Assert.Single(links);
                link.SetFocus();
                Assert.True(link.HasFocus);

                link.InvokeCommand(Command.Activate);

                Assert.Equal(target.AbsoluteUri, confirmationTarget?.AbsoluteUri);
                Assert.Equal(0, starts);
            }
            finally
            {
                application.RequestStop();
            }
        };

        application.Run(shell);
    }

    [Fact]
    public void MarkdownLink_CtrlCCopiesFocusedExactUrlThroughApplicationBinding()
    {
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        FakeClipboard clipboard = new(
            fakeClipboardThrowsNotSupportedException: false,
            isSupportedAlwaysFalse: false);
        application.Driver!.Clipboard = clipboard;
        using CancellationTokenSource shutdown = new();
        Uri target = new("https://example.com/docs?q=copy");
        TerminalChatApplication terminal = new(new TerminalOptions(TerminalLayout.Tabs, false));
        using TerminalPresenter presenter = CreatePresenter(terminal);
        using Runnable shell = terminal.CreateShell(application, presenter, shutdown);

        terminal.ApplyChatChanges([MarkdownChange("link", $"[Docs]({target.AbsoluteUri})")]);
        int attempts = 0;
        application.Iteration += (_, _) =>
        {
            ReceivedLink[] links = Descendants(shell).OfType<ReceivedLink>().ToArray();
            if (links.Length == 0 && ++attempts < 5)
            {
                return;
            }

            try
            {
                ReceivedLink link = Assert.Single(links);
                link.SetFocus();
                Assert.True(link.HasFocus);

                RaiseTerminalKey(application, Key.C.WithCtrl);

                Assert.Equal(target.AbsoluteUri, clipboard.GetClipboardData());
                Assert.Equal("Copied to clipboard.", GetStatus(shell).Content);
            }
            finally
            {
                application.RequestStop();
            }
        };

        application.Run(shell);
    }

    [Fact]
    public void CreateShell_DefaultLayoutIsBorderlessAndKeepsNavigationVisible()
    {
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        using CancellationTokenSource shutdown = new();
        TerminalChatApplication terminal = new(TerminalOptions.Parse([]));
        using TerminalPresenter presenter = CreatePresenter(terminal);

        using TerminalShellView shell = Assert.IsType<TerminalShellView>(
            terminal.CreateShell(application, presenter, shutdown));

        Assert.Equal(LineStyle.None, shell.BorderStyle);
        Assert.Equal(string.Empty, shell.Title);
        Assert.Same(shell.Navigation, shell.SubViews.First());
        Assert.DoesNotContain(
            Descendants(shell),
            view => view is Window or Tabs or FrameView or StatusBar);
        View surfaces = shell.ContentRegion;
        Assert.Equal(
            ["_Chat", "_Thoughts", "_Activities", "_Help"],
            surfaces.SubViews.Select(view => view.Title));
        AssertDefaultSurface(surfaces, "_Chat");
        AssertRequiredChatControls(shell);
    }

    [Theory]
    [InlineData(false, "* Copilot Studio  connected")]
    [InlineData(true, "● Copilot Studio  connected")]
    public void CreateShell_ConnectionHeaderUsesEncodingSelectedAgentGlyph(
        bool useUnicode,
        string expected)
    {
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        using CancellationTokenSource shutdown = new();
        Encoding outputEncoding = useUnicode ? Encoding.UTF8 : Encoding.ASCII;
        TerminalChatApplication terminal = new(
            new TerminalOptions(TerminalLayout.Tabs, false),
            confirmOpen: null,
            startProcess: _ => { },
            outputEncoding: outputEncoding);
        using TerminalPresenter presenter = CreatePresenter(terminal);

        using Runnable shell = terminal.CreateShell(application, presenter, shutdown);

        TimelineRoleLabel header = Assert.Single(
            Descendants(shell).OfType<TimelineRoleLabel>(),
            label => label.Role == TimelineRole.Agent);
        TerminalTimelineView conversation = Assert.Single(
            Descendants(shell).OfType<TerminalTimelineView>(),
            view => view.CollapseCompletedThoughts);
        Assert.Equal(expected, header.Content);
        Assert.Equal(TimelineGlyphSet.ForEncoding(outputEncoding), conversation.Glyphs);
    }

    [Fact]
    public void CreateShell_ResolvesDefaultOutputEncodingAfterDriverInitialization()
    {
        Encoding currentEncoding = Encoding.ASCII;
        TerminalChatApplication terminal = new(
            TerminalOptions.Parse([]),
            confirmOpen: null,
            startProcess: _ => { },
            outputEncodingResolver: () => currentEncoding);

        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        currentEncoding = Encoding.UTF8;
        using CancellationTokenSource shutdown = new();
        using TerminalPresenter presenter = CreatePresenter(terminal);
        using Runnable shell = terminal.CreateShell(application, presenter, shutdown);

        TerminalTimelineView conversation = Assert.Single(
            Descendants(shell).OfType<TerminalTimelineView>(),
            view => view.CollapseCompletedThoughts);
        Assert.Equal(TimelineGlyphSet.Unicode, conversation.Glyphs);
    }

    [Fact]
    public async Task DefaultLayout_ApplicationBindingsNavigateFromEveryPrimaryControl()
    {
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        using CancellationTokenSource shutdown = new();
        TerminalChatApplication terminal = new(TerminalOptions.Parse([]));
        using TerminalPresenter presenter = CreatePresenter(terminal);
        using TerminalShellView shell = Assert.IsType<TerminalShellView>(
            terminal.CreateShell(application, presenter, shutdown));
        View surfaces = shell.ContentRegion;
        View chat = Assert.Single(surfaces.SubViews, view => view.Title == "_Chat");
        View thoughts = Assert.Single(surfaces.SubViews, view => view.Title == "_Thoughts");
        View activities = Assert.Single(surfaces.SubViews, view => view.Title == "_Activities");
        View help = Assert.Single(surfaces.SubViews, view => view.Title == "_Help");
        TerminalTimelineView conversationTimeline = Assert.Single(
            Descendants(chat).OfType<TerminalTimelineView>());
        TerminalTimelineView thoughtTimeline = Assert.Single(
            Descendants(thoughts).OfType<TerminalTimelineView>());
        ListView<ActivityRecord> activityList = Assert.Single(
            Descendants(activities).OfType<ListView<ActivityRecord>>());
        TextField composer = Assert.Single(Descendants(surfaces).OfType<TextField>());
#pragma warning disable CS0618 // Task 7 requires Terminal.Gui's TextView for the JSON inspector.
        TextView json = Assert.Single(Descendants(activities).OfType<TextView>());
#pragma warning restore CS0618

        await terminal.MonitorStartupAsync(
            Task.CompletedTask,
            action => action(),
            () => throw new InvalidOperationException("Successful startup must not stop the application."),
            CancellationToken.None);
        application.Iteration += (_, _) =>
        {
            try
            {
                Assert.False(application.Keyboard.KeyBindings.TryGet(Key.D1.WithCtrl, out _));
                Assert.False(application.Keyboard.KeyBindings.TryGet(Key.D2.WithCtrl, out _));
                Assert.False(application.Keyboard.KeyBindings.TryGet(Key.D3.WithCtrl, out _));
                Assert.False(application.Keyboard.KeyBindings.TryGet(Key.D4.WithCtrl, out _));

                (TerminalSurface Surface, View Control)[] startingFocuses =
                [
                    (TerminalSurface.Chat, composer),
                    (TerminalSurface.Chat, conversationTimeline),
                    (TerminalSurface.Activities, activityList),
                    (TerminalSurface.Activities, json)
                ];

                foreach ((TerminalSurface startingSurface, View startingControl) in startingFocuses)
                {
                    shell.Show(startingSurface);
                    startingControl.SetFocus();
                    Assert.True(startingControl.HasFocus);

                    RaiseTerminalKey(application, Key.F2);
                    Assert.Equal(TerminalSurface.Thoughts, shell.ActiveSurface);
                    Assert.True(thoughtTimeline.HasFocus);

                    RaiseTerminalKey(application, Key.F3);
                    Assert.Equal(TerminalSurface.Activities, shell.ActiveSurface);
                    Assert.True(activityList.HasFocus);

                    RaiseTerminalKey(application, Key.F4);
                    Assert.Equal(TerminalSurface.Help, shell.ActiveSurface);
                    Assert.True(help.HasFocus);

                    RaiseTerminalKey(application, Key.Esc);
                    Assert.Equal(TerminalSurface.Chat, shell.ActiveSurface);
                    Assert.True(composer.HasFocus);
                }

                foreach (Key navigationKey in new[] { Key.F2, Key.F3, Key.F4 })
                {
                    RaiseTerminalKey(application, navigationKey);
                    RaiseTerminalKey(application, Key.Esc);
                    Assert.Equal(TerminalSurface.Chat, shell.ActiveSurface);
                    Assert.True(composer.HasFocus);
                }

                RaiseTerminalKey(application, Key.C.WithCtrl);
                Assert.Equal("Nothing is selected to copy.", GetStatus(shell).Content);

                RaiseTerminalKey(application, Key.Q.WithCtrl);
                Assert.True(shutdown.IsCancellationRequested);
                Assert.True(shell.StopRequested);
            }
            finally
            {
                application.RequestStop();
            }
        };

        application.Run(shell);
    }

    [Fact]
    public void CreateShell_TabsOptionUsesBorderlessSurfaceMode()
    {
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        using CancellationTokenSource shutdown = new();
        TerminalChatApplication terminal = new(TerminalOptions.Parse(["--layout", "tabs"]));
        using TerminalPresenter presenter = CreatePresenter(terminal);

        using TerminalShellView shell = Assert.IsType<TerminalShellView>(
            terminal.CreateShell(application, presenter, shutdown));

        Assert.DoesNotContain(
            Descendants(shell),
            view => view is Window or Tabs or FrameView or StatusBar);
        View surfaces = shell.ContentRegion;
        AssertDefaultSurface(surfaces, "_Chat");
        AssertRequiredChatControls(shell);
    }

    [Fact]
    public void CreateShell_DefaultChatUsesOnlyCustomChrome()
    {
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        using CancellationTokenSource shutdown = new();
        TerminalChatApplication terminal = new(TerminalOptions.Parse([]));
        using TerminalPresenter presenter = CreatePresenter(terminal);

        using TerminalShellView shell = Assert.IsType<TerminalShellView>(
            terminal.CreateShell(application, presenter, shutdown));
        IReadOnlyList<View> descendants = Descendants(shell).ToArray();

        Assert.DoesNotContain(descendants, view => view is Window);
        Assert.DoesNotContain(descendants, view => view is FrameView);
        Assert.DoesNotContain(descendants, view => view is StatusBar);
        Assert.DoesNotContain(descendants, view => view is Tabs);
        Assert.Single(descendants.OfType<TerminalNavigationView>());
        Assert.Single(descendants.OfType<TerminalComposerView>());
        Assert.Single(descendants.OfType<TerminalFooterView>());

        View chat = Assert.Single(shell.ContentRegion.SubViews, view => view.Title == "_Chat");
        View conversation = Assert.Single(chat.SubViews);
        TimelineRoleLabel header = Assert.Single(
            conversation.SubViews.OfType<TimelineRoleLabel>(),
            label => string.Equals(label.Content, "● Copilot Studio  connected", StringComparison.Ordinal));
        TerminalTimelineView timeline = Assert.Single(
            Descendants(chat).OfType<TerminalTimelineView>(),
            view => view.CollapseCompletedThoughts);

        Assert.Equal(TimelineRole.Agent, header.Role);
        Assert.Equal(TimelineGlyphSet.ForEncoding(Console.OutputEncoding), timeline.Glyphs);
    }

    [Fact]
    public void CreateShell_ActivitiesSurfaceWrapsControlsInResponsiveInspectorView()
    {
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        using CancellationTokenSource shutdown = new();
        TerminalChatApplication terminal = new(TerminalOptions.Parse([]));
        using TerminalPresenter presenter = CreatePresenter(terminal);

        using TerminalShellView shell = Assert.IsType<TerminalShellView>(
            terminal.CreateShell(application, presenter, shutdown));
        View activities = Assert.Single(shell.ContentRegion.SubViews, view => view.Title == "_Activities");

        Assert.IsType<TerminalActivityView>(Assert.Single(activities.SubViews));
    }

    [Fact]
    public void SplitLayoutUsesPersistentNavigationAndBorderlessSurfaces()
    {
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        using CancellationTokenSource shutdown = new();
        TerminalChatApplication terminal = new(new TerminalOptions(TerminalLayout.Split, false));
        using TerminalPresenter presenter = CreatePresenter(terminal);

        using TerminalShellView shell = Assert.IsType<TerminalShellView>(
            terminal.CreateShell(application, presenter, shutdown));

        Assert.DoesNotContain(
            Descendants(shell),
            view => view is Window or Tabs or FrameView or StatusBar);
        View split = Assert.Single(shell.ContentRegion.SubViews, view => view.Visible);
        Assert.Equal("_Split", split.Title);
        Assert.Equal(["_Left", "_Activities"], split.SubViews.Select(view => view.Title));
        Assert.All(split.SubViews, view => Assert.Equal(LineStyle.None, view.BorderStyle));
        AssertRequiredChatControls(shell);

        View left = Assert.Single(split.SubViews, view => view.Title == "_Left");
        View chat = Assert.Single(left.SubViews, view => view.Title == "_Chat");
        View thoughts = Assert.Single(left.SubViews, view => view.Title == "_Thoughts");
        View activities = Assert.Single(split.SubViews, view => view.Title == "_Activities");
        TerminalTimelineView conversationTimeline = Assert.Single(
            Descendants(chat).OfType<TerminalTimelineView>());
        TerminalTimelineView thoughtTimeline = Assert.Single(
            Descendants(thoughts).OfType<TerminalTimelineView>());
        ListView<ActivityRecord> activityList = Assert.Single(
            Descendants(activities).OfType<ListView<ActivityRecord>>());
        View help = Assert.Single(shell.ContentRegion.SubViews, view => view.Title == "_Help");

        application.Iteration += (_, _) =>
        {
            try
            {
                AssertNavigation(shell, TerminalSurface.Chat);
                Assert.True(shell.Navigation.Visible);
                Assert.True(split.Visible);
                Assert.True(chat.Visible);
                Assert.False(thoughts.Visible);
                Assert.True(activities.Visible);

                RaiseTerminalKey(application, Key.F3);
                RaiseTerminalKey(application, Key.F1);
                AssertNavigation(shell, TerminalSurface.Chat);
                Assert.True(shell.Navigation.Visible);
                Assert.True(split.Visible);
                Assert.True(chat.Visible);
                Assert.False(thoughts.Visible);
                Assert.True(activities.Visible);
                Assert.True(conversationTimeline.HasFocus);

                RaiseTerminalKey(application, Key.F2);
                AssertNavigation(shell, TerminalSurface.Thoughts);
                Assert.True(shell.Navigation.Visible);
                Assert.True(split.Visible);
                Assert.False(chat.Visible);
                Assert.True(thoughts.Visible);
                Assert.True(activities.Visible);
                Assert.True(thoughtTimeline.HasFocus);

                RaiseTerminalKey(application, Key.F3);
                AssertNavigation(shell, TerminalSurface.Activities);
                Assert.True(shell.Navigation.Visible);
                Assert.True(split.Visible);
                Assert.False(chat.Visible);
                Assert.True(thoughts.Visible);
                Assert.True(activities.Visible);
                Assert.True(activityList.HasFocus);

                RaiseTerminalKey(application, Key.F4);
                AssertNavigation(shell, TerminalSurface.Help);
                Assert.True(shell.Navigation.Visible);
                Assert.False(split.Visible);
                Assert.True(help.Visible);
                Assert.True(help.HasFocus);

                RaiseTerminalKey(application, Key.Esc);
                AssertNavigation(shell, TerminalSurface.Chat);
                Assert.True(shell.Navigation.Visible);
                Assert.True(split.Visible);
                Assert.True(chat.Visible);
                Assert.False(thoughts.Visible);
                Assert.True(activities.Visible);
                Assert.True(conversationTimeline.HasFocus);
            }
            finally
            {
                application.RequestStop();
            }
        };

        application.Run(shell);
    }

    [Fact]
    public async Task SplitLayout_ApplicationBindingsSwitchLeftContentAndPreserveActivities()
    {
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        using CancellationTokenSource shutdown = new();
        TerminalChatApplication terminal = new(new TerminalOptions(TerminalLayout.Split, false));
        using TerminalPresenter presenter = CreatePresenter(terminal);
        using TerminalShellView shell = Assert.IsType<TerminalShellView>(
            terminal.CreateShell(application, presenter, shutdown));
        View split = Assert.Single(shell.ContentRegion.SubViews, view => view.Title == "_Split");
        View left = Assert.Single(split.SubViews, view => view.Title == "_Left");
        View chat = Assert.Single(left.SubViews, view => view.Title == "_Chat");
        View thoughts = Assert.Single(left.SubViews, view => view.Title == "_Thoughts");
        View activities = Assert.Single(split.SubViews, view => view.Title == "_Activities");
        TextField composer = Assert.Single(Descendants(chat).OfType<TextField>());
        TerminalTimelineView thoughtTimeline = Assert.Single(
            Descendants(thoughts).OfType<TerminalTimelineView>());
        ListView<ActivityRecord> activityList = Assert.Single(
            Descendants(activities).OfType<ListView<ActivityRecord>>());
        View help = Assert.Single(shell.ContentRegion.SubViews, view => view.Title == "_Help");

        await terminal.MonitorStartupAsync(
            Task.CompletedTask,
            action => action(),
            () => throw new InvalidOperationException("Successful startup must not stop the application."),
            CancellationToken.None);
        application.Iteration += (_, _) =>
        {
            RaiseTerminalKey(application, Key.F2);
            Assert.True(split.Visible);
            Assert.False(chat.Visible);
            Assert.True(thoughts.Visible);
            Assert.True(activities.Visible);
            Assert.True(thoughtTimeline.HasFocus);

            RaiseTerminalKey(application, Key.F3);
            Assert.True(thoughts.Visible);
            Assert.True(activities.Visible);
            Assert.True(activityList.HasFocus);

            RaiseTerminalKey(application, Key.F4);
            Assert.False(split.Visible);
            Assert.True(help.Visible);
            Assert.True(help.HasFocus);

            RaiseTerminalKey(application, Key.Esc);
            Assert.True(split.Visible);
            Assert.True(chat.Visible);
            Assert.False(thoughts.Visible);
            Assert.True(activities.Visible);
            Assert.True(composer.HasFocus);
            application.RequestStop();
        };

        application.Run(shell);
    }

    [Fact]
    public void NarrowResizeKeepsChatAndActivitiesWithinViewport()
    {
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        using CancellationTokenSource shutdown = new();
        TerminalChatApplication terminal = new(new TerminalOptions(TerminalLayout.Split, false));
        using TerminalPresenter presenter = CreatePresenter(terminal);
        using TerminalShellView shell = Assert.IsType<TerminalShellView>(
            terminal.CreateShell(application, presenter, shutdown));
        View split = Assert.Single(shell.ContentRegion.SubViews, view => view.Title == "_Split");
        View left = Assert.Single(split.SubViews, view => view.Title == "_Left");
        View chat = Assert.Single(left.SubViews, view => view.Title == "_Chat");
        View thoughts = Assert.Single(left.SubViews, view => view.Title == "_Thoughts");
        View activities = Assert.Single(split.SubViews, view => view.Title == "_Activities");
        TerminalTimelineView conversationTimeline = Assert.Single(
            Descendants(chat).OfType<TerminalTimelineView>());
        TerminalTimelineView thoughtTimeline = Assert.Single(
            Descendants(thoughts).OfType<TerminalTimelineView>());
#pragma warning disable CS0618 // Task 7 requires Terminal.Gui's TextView for the JSON inspector.
        TextView json = Assert.Single(Descendants(activities).OfType<TextView>());
#pragma warning restore CS0618
        ListView<ActivityRecord> activityList = Assert.Single(
            Descendants(activities).OfType<ListView<ActivityRecord>>());

        terminal.ApplyChatChanges(
        [
            new ChatChange(
                ChatChangeKind.Upsert,
                "chat",
                new ChatEntry(
                    "chat",
                    ChatEntryKind.Agent,
                    "Agent",
                    "**Hello** narrow world 界界 🙂🙂\twith controls \u001B",
                    false,
                    [],
                    [],
                    "chat")),
            new ChatChange(
                ChatChangeKind.Upsert,
                "thought",
                new ChatEntry(
                    "thought",
                    ChatEntryKind.Thought,
                    "Reasoning",
                    "Inspecting responsive split layout",
                    true,
                    [],
                    [],
                    "thought"))
        ]);
        terminal.AddActivity(new ActivityRecord(
            1,
            ActivityDirection.Inbound,
            DateTimeOffset.Parse("2026-09-13T12:00:00Z"),
            ActivityTypes.Message,
            "message activity",
            new Activity { Type = ActivityTypes.Message, Text = "hello" },
            """{"type":"message","text":"hello"}""",
            null));

        foreach (int width in new[] { 20, 40, 71, 72, 100 })
        {
            foreach (int height in new[] { 6, 10, 24 })
            {
                shell.Frame = new System.Drawing.Rectangle(0, 0, width, height);
                Assert.True(shell.Layout(), $"Layout failed for {width}x{height}.");

                AssertNavigation(shell, TerminalSurface.Chat);
                Assert.True(split.Visible, $"Split hidden at {width}x{height}.");
                Assert.True(chat.Visible, $"Chat hidden at {width}x{height}.");
                Assert.False(thoughts.Visible, $"Thoughts visible while Chat is active at {width}x{height}.");
                Assert.True(activities.Visible, $"Activities hidden at {width}x{height}.");
                AssertWithinParent(shell);

                Assert.All(
                    conversationTimeline.RenderedLines,
                    line => Assert.True(
                        PlainText(line).GetColumns() <= Math.Max(1, conversationTimeline.Frame.Width),
                        $"Conversation line '{PlainText(line)}' exceeds {conversationTimeline.Frame.Width} cells at {width}x{height}."));
                Assert.True(activityList.Frame.Width > 0, $"Activity list has no width at {width}x{height}.");
                Assert.True(activityList.Frame.Height > 0, $"Activity list has no height at {width}x{height}.");
                Assert.True(json.Frame.Width > 0, $"JSON inspector has no width at {width}x{height}.");
                Assert.True(json.Frame.Height > 0, $"JSON inspector has no height at {width}x{height}.");

                shell.Show(TerminalSurface.Thoughts);
                Assert.True(shell.Layout(), $"Thought layout failed for {width}x{height}.");
                AssertNavigation(shell, TerminalSurface.Thoughts);
                Assert.True(split.Visible, $"Split hidden after F2 equivalent at {width}x{height}.");
                Assert.False(chat.Visible, $"Chat visible while Thoughts is active at {width}x{height}.");
                Assert.True(thoughts.Visible, $"Thoughts hidden at {width}x{height}.");
                Assert.True(activities.Visible, $"Activities hidden while Thoughts is active at {width}x{height}.");
                AssertWithinParent(shell);
                Assert.All(
                    thoughtTimeline.RenderedLines,
                    line => Assert.True(
                        PlainText(line).GetColumns() <= Math.Max(1, thoughtTimeline.Frame.Width),
                        $"Thought line '{PlainText(line)}' exceeds {thoughtTimeline.Frame.Width} cells at {width}x{height}."));

                shell.Show(TerminalSurface.Activities);
                Assert.True(shell.Layout(), $"Activities layout failed for {width}x{height}.");
                AssertNavigation(shell, TerminalSurface.Activities);
                Assert.True(split.Visible, $"Split hidden after F3 equivalent at {width}x{height}.");
                Assert.True(activities.Visible, $"Activities hidden after F3 equivalent at {width}x{height}.");
                AssertWithinParent(shell);
                Assert.True(activityList.Frame.Width > 0, $"Activity list has no width after F3 equivalent at {width}x{height}.");
                Assert.True(activityList.Frame.Height > 0, $"Activity list has no height after F3 equivalent at {width}x{height}.");
                Assert.True(json.Frame.Width > 0, $"JSON inspector has no width after F3 equivalent at {width}x{height}.");
                Assert.True(json.Frame.Height > 0, $"JSON inspector has no height after F3 equivalent at {width}x{height}.");

                shell.Show(TerminalSurface.Chat);
            }
        }
    }

    [Fact]
    public void ApplyChatChanges_ShowsThoughtsOnlyInInspector()
    {
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        using CancellationTokenSource shutdown = new();
        TerminalChatApplication terminal = new(new TerminalOptions(TerminalLayout.Tabs, false));
        using TerminalPresenter presenter = CreatePresenter(terminal);
        using Runnable shell = terminal.CreateShell(application, presenter, shutdown);
        View surfaces = Assert.IsType<TerminalShellView>(shell).ContentRegion;
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
        RunOneIteration(application, shell);

        TerminalTimelineView conversation = Assert.Single(
            Descendants(chat).OfType<TerminalTimelineView>(),
            view => view.CollapseCompletedThoughts);
        TerminalTimelineView thoughtInspector = Assert.Single(
            Descendants(thoughts).OfType<TerminalTimelineView>(),
            view => !view.CollapseCompletedThoughts);

        Assert.DoesNotContain(conversation.RenderedLines, line => PlainText(line) == "Checking account");
        Assert.DoesNotContain(conversation.RenderedLines, line => PlainText(line) == "Compared all records");
        Assert.DoesNotContain(
            conversation.RenderedLines,
            line => PlainText(line) == "Reasoning complete · F2 for details");
        Assert.Contains(thoughtInspector.RenderedLines, line => PlainText(line) == "Checking account");
        Assert.Contains(thoughtInspector.RenderedLines, line => PlainText(line) == "Compared all records");
    }

    [Fact]
    public void ApplyChatChanges_RoutesToolCallsOnlyToThoughts()
    {
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        using CancellationTokenSource shutdown = new();
        TerminalChatApplication terminal = new(new TerminalOptions(TerminalLayout.Tabs, false));
        using TerminalPresenter presenter = CreatePresenter(terminal);
        using Runnable shell = terminal.CreateShell(application, presenter, shutdown);
        View surfaces = Assert.IsType<TerminalShellView>(shell).ContentRegion;
        View chat = Assert.Single(surfaces.SubViews, view => view.Title == "_Chat");
        View thoughts = Assert.Single(surfaces.SubViews, view => view.Title == "_Thoughts");
        TerminalTimelineView chatTimeline = Assert.Single(Descendants(chat).OfType<TerminalTimelineView>());
        TerminalTimelineView thoughtTimeline = Assert.Single(
            Descendants(thoughts).OfType<TerminalTimelineView>());

        terminal.ApplyChatChanges(
        [
            ToolChange("tool:1", "started"),
            ToolChange("tool:1", "completed")
        ]);
        RunOneIteration(application, shell);

        Assert.DoesNotContain(chatTimeline.RenderedLines, line => line.EntryKey == "tool:1");
        Assert.Equal(
            ["tool:1"],
            thoughtTimeline.RenderedLines.Select(line => line.EntryKey).Distinct());
    }

    [Fact]
    public void ApplyChatChanges_InterpreterLifecycleUpdatesOneThoughtToolBlock()
    {
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        using CancellationTokenSource shutdown = new();
        TerminalChatApplication terminal = new(new TerminalOptions(TerminalLayout.Tabs, false));
        using TerminalPresenter presenter = CreatePresenter(terminal);
        using Runnable shell = terminal.CreateShell(application, presenter, shutdown);
        View surfaces = Assert.IsType<TerminalShellView>(shell).ContentRegion;
        View chat = Assert.Single(surfaces.SubViews, view => view.Title == "_Chat");
        View thoughts = Assert.Single(surfaces.SubViews, view => view.Title == "_Thoughts");
        TerminalTimelineView chatTimeline = Assert.Single(Descendants(chat).OfType<TerminalTimelineView>());
        TerminalTimelineView thoughtTimeline = Assert.Single(Descendants(thoughts).OfType<TerminalTimelineView>());
        ActivityInterpreter interpreter = new();

        terminal.ApplyChatChanges(interpreter.Process(StartedWeatherActivity(), ActivityDirection.Inbound));
        RunOneIteration(application, shell);
        Assert.Contains(thoughtTimeline.RenderedLines, line => PlainText(line).Contains("Running", StringComparison.Ordinal));

        terminal.ApplyChatChanges(interpreter.Process(CompletedWeatherActivity(), ActivityDirection.Inbound));
        RunOneIteration(application, shell);

        Assert.DoesNotContain(chatTimeline.RenderedLines, line => line.EntryKey == "tool:toolu_01EAp1krYNiK2odqQv9mu7hn");
        Assert.Equal(
            ["tool:toolu_01EAp1krYNiK2odqQv9mu7hn"],
            thoughtTimeline.RenderedLines
                .Where(line => line.EntryKey == "tool:toolu_01EAp1krYNiK2odqQv9mu7hn")
                .Select(line => line.EntryKey)
                .Distinct());
        Assert.Contains(thoughtTimeline.RenderedLines, line => PlainText(line).Contains("Completed in 2.97 s", StringComparison.Ordinal));
        Assert.DoesNotContain(thoughtTimeline.RenderedLines, line => PlainText(line).Contains("Running", StringComparison.Ordinal));
    }

    [Fact]
    public void ApplyChatChanges_InterpreterStreamToolCallCoexistsWithStatusThoughtsAndAttachments()
    {
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        using CancellationTokenSource shutdown = new();
        TerminalChatApplication terminal = new(new TerminalOptions(TerminalLayout.Tabs, false));
        using TerminalPresenter presenter = CreatePresenter(terminal);
        using Runnable shell = terminal.CreateShell(application, presenter, shutdown);
        View surfaces = Assert.IsType<TerminalShellView>(shell).ContentRegion;
        View chat = Assert.Single(surfaces.SubViews, view => view.Title == "_Chat");
        View thoughts = Assert.Single(surfaces.SubViews, view => view.Title == "_Thoughts");
        TerminalTimelineView chatTimeline = Assert.Single(Descendants(chat).OfType<TerminalTimelineView>());
        TerminalTimelineView thoughtTimeline = Assert.Single(Descendants(thoughts).OfType<TerminalTimelineView>());
        ActivityInterpreter interpreter = new();

        terminal.ApplyChatChanges(interpreter.Process(StreamWeatherActivity(), ActivityDirection.Inbound));
        RunOneIteration(application, shell);

        Assert.Contains(chatTimeline.RenderedLines, line => PlainText(line) == "Calling current_weather...");
        Assert.Contains(chatTimeline.RenderedLines, line => PlainText(line).Contains("report.csv", StringComparison.Ordinal));
        Assert.DoesNotContain(chatTimeline.RenderedLines, line => line.EntryKey == "tool:toolu_01EAp1krYNiK2odqQv9mu7hn");
        Assert.Contains(thoughtTimeline.RenderedLines, line => PlainText(line) == "Checking weather");
        Assert.Contains(thoughtTimeline.RenderedLines, line => line.EntryKey == "tool:toolu_01EAp1krYNiK2odqQv9mu7hn");
        Assert.Contains(thoughtTimeline.RenderedLines, line => PlainText(line).Contains("Waiting for", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData((int)DiagnosticSeverity.Information, "Ready.", (int)TimelineRole.Muted)]
    [InlineData((int)DiagnosticSeverity.Warning, "Warning: Reconnecting", (int)TimelineRole.Warning)]
    [InlineData((int)DiagnosticSeverity.Error, "Error: Connection lost", (int)TimelineRole.Error)]
    public void SetStatus_UsesSemanticStatusRoles(
        int severityValue,
        string expectedText,
        int expectedRoleValue)
    {
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        using CancellationTokenSource shutdown = new();
        TerminalChatApplication terminal = new(TerminalOptions.Parse([]));
        using TerminalPresenter presenter = CreatePresenter(terminal);
        using Runnable shell = terminal.CreateShell(application, presenter, shutdown);
        DiagnosticSeverity severity = (DiagnosticSeverity)severityValue;
        TimelineRole expectedRole = (TimelineRole)expectedRoleValue;

        string text = severity switch
        {
            DiagnosticSeverity.Warning => "Reconnecting",
            DiagnosticSeverity.Error => "Connection lost",
            _ => "Ready."
        };

        terminal.SetStatus(text, severity);
        RunOneIteration(application, shell);

        TimelineRoleLabel status = GetStatus(shell);

        Assert.Equal(expectedText, status.Content);
        Assert.Equal(expectedRole, status.Role);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public void CreateShell_DefaultLayoutKeepsTranscriptVisibleWithoutOverlappingBottomStackAtShortHeights(int height)
    {
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        using CancellationTokenSource shutdown = new();
        TerminalChatApplication terminal = new(TerminalOptions.Parse([]));
        using TerminalPresenter presenter = CreatePresenter(terminal);
        using Runnable shell = terminal.CreateShell(application, presenter, shutdown);

        View conversation = GetChatConversation(shell);

        conversation.Frame = new System.Drawing.Rectangle(0, 0, 50, height);
        Assert.True(conversation.Layout());

        TimelineRoleLabel[] labels = conversation.SubViews.OfType<TimelineRoleLabel>().ToArray();
        TimelineRoleLabel header = Assert.Single(labels, label => label.Role == TimelineRole.Agent);
        TimelineRoleLabel status = Assert.Single(labels, label => !ReferenceEquals(label, header));
        TerminalTimelineView transcript = Assert.Single(conversation.SubViews.OfType<TerminalTimelineView>());
        TerminalComposerView composer = Assert.Single(conversation.SubViews.OfType<TerminalComposerView>());
        TerminalFooterView footer = Assert.Single(conversation.SubViews.OfType<TerminalFooterView>());
        View actionBar = Assert.Single(
            conversation.SubViews,
            view => view is not TimelineRoleLabel
                && view is not TerminalTimelineView
                && view is not Label
                && view is not TerminalComposerView
                && view is not TerminalFooterView);

        Assert.True(transcript.Frame.Height >= 1, $"Height {height}: transcript frame {transcript.Frame}");
        Assert.True(
            header.Frame.Y + header.Frame.Height <= transcript.Frame.Y,
            $"Height {height}: header {header.Frame} overlaps transcript {transcript.Frame}");
        Assert.True(
            transcript.Frame.Y + transcript.Frame.Height <= actionBar.Frame.Y,
            $"Height {height}: transcript {transcript.Frame} overlaps action bar {actionBar.Frame}");
        Assert.True(
            actionBar.Frame.Y + actionBar.Frame.Height <= status.Frame.Y,
            $"Height {height}: action bar {actionBar.Frame} overlaps status {status.Frame}");
        Assert.True(
            status.Frame.Y + status.Frame.Height <= composer.Frame.Y,
            $"Height {height}: status {status.Frame} overlaps composer {composer.Frame}");
        Assert.True(
            composer.Frame.Y + composer.Frame.Height <= footer.Frame.Y,
            $"Height {height}: composer {composer.Frame} overlaps footer {footer.Frame}");
        Assert.True(
            footer.Frame.Y + footer.Frame.Height <= conversation.Frame.Height,
            $"Height {height}: footer {footer.Frame} exceeds conversation height {conversation.Frame.Height}");
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
        using Runnable shell = terminal.CreateShell(application, presenter, shutdown);
        TextField composer = Assert.Single(Descendants(shell).OfType<TextField>());

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
        application.Init(DriverRegistry.Names.ANSI);
        using CancellationTokenSource shutdown = new();
        TerminalChatApplication terminal = new(new TerminalOptions(TerminalLayout.Tabs, false));
        using TerminalPresenter presenter = CreatePresenter(terminal);
        using Runnable shell = terminal.CreateShell(application, presenter, shutdown);
        TextField composer = Assert.Single(Descendants(shell).OfType<TextField>());
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
        using Runnable shell = terminal.CreateShell(application, presenter, shutdown);
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
        RunOneIteration(application, shell);

        Assert.Equal(
            ["https://first.example/", "https://second.example/"],
            Descendants(shell).OfType<ReceivedLink>().Select(link => link.Target.AbsoluteUri));
        Button action = Assert.Single(Descendants(shell).OfType<Button>());
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
        using Runnable shell = terminal.CreateShell(application, presenter, shutdown);
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
        RunOneIteration(application, shell);

        Button action = Assert.Single(Descendants(shell).OfType<Button>());
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
        using Runnable shell = terminal.CreateShell(application, presenter, shutdown);
        TextField composer = Assert.Single(Descendants(shell).OfType<TextField>());

        await terminal.MonitorStartupAsync(
            Task.CompletedTask,
            action => action(),
            () => throw new InvalidOperationException("Successful startup must not stop the application."),
            CancellationToken.None);
        terminal.ApplyChatChanges(
        [
            ActionableChange("entry", "Send action", "send")
        ]);
        RunOneIteration(application, shell);
        Assert.Single(Descendants(shell).OfType<Button>());

        composer.Value = "new request";
        terminal.SubmitComposer();
        await client.ExecuteStarted!.Task;

        Assert.Empty(Descendants(shell).OfType<Button>());
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
        Assert.Single(descendants.OfType<TerminalComposerView>());
        Assert.Single(descendants.OfType<TerminalFooterView>());

#pragma warning disable CS0618 // Task 7 requires Terminal.Gui's TextView for the JSON inspector.
        TextView json = Assert.Single(descendants.OfType<TextView>());
#pragma warning restore CS0618
        Assert.True(json.ReadOnly);
        Assert.True(json.ScrollBars);
        Assert.False(descendants.OfType<TextField>().Single().Enabled);
        Assert.Equal("Ready.", GetStatus(root).Content);
    }

    private static void AssertNavigation(TerminalShellView shell, TerminalSurface activeSurface)
    {
        Assert.Equal(activeSurface, shell.ActiveSurface);
        Assert.Equal(activeSurface, shell.Navigation.ActiveSurface);
        Assert.Same(shell.Navigation, shell.SubViews.First());
    }

    private static void AssertWithinParent(View parent)
    {
        foreach (View child in parent.SubViews)
        {
            Assert.True(child.Frame.X >= 0, $"{child.Title} has negative X in {parent.Title}: {child.Frame}");
            Assert.True(child.Frame.Y >= 0, $"{child.Title} has negative Y in {parent.Title}: {child.Frame}");
            Assert.True(child.Frame.Width >= 0, $"{child.Title} has negative width in {parent.Title}: {child.Frame}");
            Assert.True(child.Frame.Height >= 0, $"{child.Title} has negative height in {parent.Title}: {child.Frame}");
            Assert.True(
                child.Frame.X + child.Frame.Width <= Math.Max(0, parent.Frame.Width),
                $"{child.Title} exceeds parent width {parent.Frame.Width} in {parent.Title}: {child.Frame}");
            Assert.True(
                child.Frame.Y + child.Frame.Height <= Math.Max(0, parent.Frame.Height),
                $"{child.Title} exceeds parent height {parent.Frame.Height} in {parent.Title}: {child.Frame}");
            AssertWithinParent(child);
        }
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

    private static ChatChange MarkdownChange(string key, string text, bool isTransient = false)
    {
        return new ChatChange(
            ChatChangeKind.Upsert,
            key,
            new ChatEntry(
                key,
                ChatEntryKind.Agent,
                "Agent",
                text,
                isTransient,
                [],
                [],
                key));
    }

    private static ChatChange ToolChange(string key, string status)
    {
        return new ChatChange(
            ChatChangeKind.Upsert,
            key,
            new ChatEntry(
                key,
                ChatEntryKind.ToolCall,
                "Agent",
                string.Empty,
                !string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase),
                [],
                [],
                "stream-1",
                ToolCall: new ToolCallDetails(
                    key["tool:".Length..],
                    "current_weather",
                    "Get current weather",
                    "Connector",
                    status,
                    [],
                    [],
                    null)));
    }

    private static Activity StartedWeatherActivity()
    {
        return ToolCallActivity(
            ToolCallEntity(
                "started",
                JsonSerializer.SerializeToElement(new { Location = "Seattle, WA, USA", units = "I" }),
                JsonSerializer.SerializeToElement(Array.Empty<string>())));
    }

    private static Activity CompletedWeatherActivity()
    {
        return ToolCallActivity(
            ToolCallEntity(
                "completed",
                JsonSerializer.SerializeToElement(new { Location = "Seattle, WA, USA", units = "I" }),
                JsonSerializer.SerializeToElement(Array.Empty<string>()),
                durationMs: 2971));
    }

    private static Activity StreamWeatherActivity()
    {
        Activity activity = StreamActivity(
            ActivityTypes.Typing,
            "stream-1",
            "Calling current_weather...",
            null,
            StreamTypes.Informative,
            1);
        activity.Entities!.Insert(0, ToolCallEntity(
            "started",
            JsonSerializer.SerializeToElement(new { Location = "Seattle" }),
            JsonSerializer.SerializeToElement(new[] { "units" })));
        activity.Entities.Add(
            new Entity("thought")
            {
                Properties = { ["content"] = JsonSerializer.SerializeToElement("Checking weather") }
            });
        activity.Attachments =
        [
            new Attachment
            {
                Name = "report.csv",
                ContentType = "text/csv",
                ContentUrl = "https://files.example/report.csv"
            }
        ];

        return activity;
    }

    private static Activity ToolCallActivity(Entity toolCallEntity)
    {
        return new Activity
        {
            Type = ActivityTypes.Message,
            Entities =
            [
                toolCallEntity
            ]
        };
    }

    private static Activity StreamActivity(
        string activityType,
        string activityId,
        string text,
        string? streamId,
        string streamType,
        int? sequence,
        string? streamResult = null)
    {
        return new Activity
        {
            Type = activityType,
            Id = activityId,
            Text = text,
            Entities =
            [
                new StreamInfo
                {
                    StreamId = streamId!,
                    StreamType = streamType,
                    StreamSequence = sequence,
                    StreamResult = streamResult!
                }
            ]
        };
    }

    private static Entity ToolCallEntity(
        string status,
        JsonElement filledParameters,
        JsonElement unfilledParameters,
        long? durationMs = null)
    {
        Entity entity = new("toolCall")
        {
            Properties =
            {
                ["toolCallId"] = JsonSerializer.SerializeToElement("toolu_01EAp1krYNiK2odqQv9mu7hn"),
                ["toolName"] = JsonSerializer.SerializeToElement("current_weather"),
                ["toolDisplayName"] = JsonSerializer.SerializeToElement("Get current weather"),
                ["toolCategory"] = JsonSerializer.SerializeToElement("Connector"),
                ["status"] = JsonSerializer.SerializeToElement(status),
                ["filledParameters"] = filledParameters,
                ["unfilledParameters"] = unfilledParameters,
                ["hiddenFilledParameters"] = JsonSerializer.SerializeToElement(new { apiKey = "must-not-render" }),
                ["hiddenUnfilledParameters"] = JsonSerializer.SerializeToElement(new[] { "secret" }),
                ["result"] = JsonSerializer.SerializeToElement("must-not-render")
            }
        };

        if (durationMs is not null)
        {
            entity.Properties["durationMs"] = JsonSerializer.SerializeToElement(durationMs.Value);
        }

        return entity;
    }

    private static void RunOneIteration(IApplication application, Runnable shell)
    {
        application.StopAfterFirstIteration = true;
        application.Run(shell);
    }

    private static void RaiseTerminalKey(IApplication application, Key key)
    {
        bool handled = application.Keyboard.RaiseKeyDownEvent(key);
        Assert.True(handled, $"{key} was not handled by the application binding.");
    }

    private static View GetChatConversation(View root)
    {
        View chat = Assert.Single(Descendants(root), view => view.Title == "_Chat");
        return Assert.Single(chat.SubViews);
    }

    private static TimelineRoleLabel GetStatus(View root)
    {
        TimelineRoleLabel[] labels = GetChatConversation(root).SubViews.OfType<TimelineRoleLabel>().ToArray();
        Assert.Equal(2, labels.Length);
        return Assert.Single(
            labels,
            label => !label.Content.Contains("Copilot Studio", StringComparison.Ordinal));
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
