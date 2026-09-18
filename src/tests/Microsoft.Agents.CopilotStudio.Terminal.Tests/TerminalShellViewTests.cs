#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

[Collection("TerminalGui")]
public sealed class TerminalShellViewTests
{
    [Fact]
    public void Shell_IsBorderlessAndKeepsNavigationOutsideSwitchableContent()
    {
        using TerminalShellView shell = CreateShell();

        Assert.Equal(LineStyle.None, shell.BorderStyle);
        Assert.Equal(string.Empty, shell.Title);
        Assert.Same(shell.Navigation, shell.SubViews.First());
        Assert.Contains(shell.ContentRegion, shell.SubViews);

        foreach (TerminalSurface surface in Enum.GetValues<TerminalSurface>())
        {
            shell.Show(surface);

            Assert.True(shell.Navigation.Visible);
            Assert.Equal(surface, shell.ActiveSurface);
            Assert.Single(shell.ContentRegion.SubViews, view => view.Visible);
        }
    }

    [Fact]
    public void Navigation_InvalidFocusTransitionReportsFailure()
    {
        InvalidOperationException failure = new("focus failed");
        Exception? reported = null;
        using TerminalShellView shell = CreateShell(
            resolveFocusTarget: _ => throw failure,
            reportNavigationFailure: exception => reported = exception);

        shell.Show(TerminalSurface.Thoughts);

        Assert.Same(failure, reported);
    }

    [Fact]
    public void Navigation_DisposedFocusTransitionReportsFailure()
    {
        ObjectDisposedException failure = new("focus target");
        Exception? reported = null;
        using TerminalShellView shell = CreateShell(
            resolveFocusTarget: _ => throw failure,
            reportNavigationFailure: exception => reported = exception);

        shell.Show(TerminalSurface.Activities);

        Assert.Same(failure, reported);
    }

    [Fact]
    public void Navigation_RequestsFullRefreshAfterChangingSurface()
    {
        int refreshRequests = 0;
        using TerminalShellView shell = CreateShell(
            requestFullRefresh: () => refreshRequests++);
        int requestsBeforeNavigation = refreshRequests;

        shell.Show(TerminalSurface.Activities);

        Assert.Equal(requestsBeforeNavigation + 1, refreshRequests);
    }

    [Fact]
    public void Navigation_NotifiesActiveSurfaceWithoutInspectingViewHierarchy()
    {
        List<TerminalSurface> changes = [];
        using TerminalShellView shell = CreateShell(
            activeSurfaceChanged: changes.Add);
        changes.Clear();

        shell.Show(TerminalSurface.Activities);
        shell.Show(TerminalSurface.Chat);

        Assert.Equal([TerminalSurface.Activities, TerminalSurface.Chat], changes);
    }

    [Fact]
    public void Navigation_UnexpectedFocusTransitionFailurePropagates()
    {
        NotSupportedException failure = new("unexpected");
        using TerminalShellView shell = CreateShell(
            resolveFocusTarget: _ => throw failure);

        NotSupportedException thrown = Assert.Throws<NotSupportedException>(
            () => shell.Show(TerminalSurface.Help));

        Assert.Same(failure, thrown);
    }

    [Fact]
    public void Cancel_CommandRemainsUnhandledWhenChatIsAlreadyActive()
    {
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        using TerminalShellView shell = CreateShell();
        SessionToken shellSession = Assert.IsType<SessionToken>(application.Begin(shell));

        try
        {
            bool? handled = shell.InvokeCommand(Command.Cancel);

            Assert.False(handled);
            Assert.Equal(TerminalSurface.Chat, shell.ActiveSurface);
        }
        finally
        {
            application.End(shellSession);
        }
    }

    [Fact]
    public void Modal_EscapeStopsModalWithoutChangingBackgroundShell()
    {
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        using TerminalShellView shell = CreateShell();
        using Dialog modal = new();
        shell.Show(TerminalSurface.Thoughts);
        shell.RegisterApplicationBindings(application);
        SessionToken shellSession = Assert.IsType<SessionToken>(application.Begin(shell));
        SessionToken modalSession = Assert.IsType<SessionToken>(application.Begin(modal));

        try
        {
            Assert.Same(modal, application.TopRunnable);

            bool handled = application.Keyboard.RaiseKeyDownEvent(Key.Esc);

            Assert.True(handled);
            Assert.True(modal.StopRequested);
            Assert.False(shell.StopRequested);
            Assert.Equal(TerminalSurface.Thoughts, shell.ActiveSurface);
        }
        finally
        {
            application.End(modalSession);
            application.End(shellSession);
        }
    }

    [Theory]
    [MemberData(nameof(ModalShellCommandKeys))]
    public void Modal_ShellApplicationCommandDoesNotReachBackgroundShell(
        Key key,
        int initialSurfaceValue)
    {
        TerminalSurface initialSurface = (TerminalSurface)initialSurfaceValue;
        int shellCopies = 0;
        int shellQuits = 0;
        int shellSaves = 0;
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        using TerminalShellView shell = CreateShell(
            copy: () => shellCopies++,
            quit: () => shellQuits++,
            saveActivities: () => shellSaves++);
        using Dialog modal = new();
        shell.Show(initialSurface);
        shell.RegisterApplicationBindings(application);
        SessionToken shellSession = Assert.IsType<SessionToken>(application.Begin(shell));
        SessionToken modalSession = Assert.IsType<SessionToken>(application.Begin(modal));

        try
        {
            Assert.Same(modal, application.TopRunnable);
            bool handled = application.Keyboard.RaiseKeyDownEvent(key);

            Assert.False(handled);
            Assert.Equal(0, shellCopies);
            Assert.Equal(0, shellQuits);
            Assert.Equal(0, shellSaves);
            Assert.Equal(initialSurface, shell.ActiveSurface);
        }
        finally
        {
            application.End(modalSession);
            application.End(shellSession);
        }
    }

    public static TheoryData<Key, int> ModalShellCommandKeys =>
        new()
        {
            { Key.F1, (int)TerminalSurface.Help },
            { Key.F2, (int)TerminalSurface.Help },
            { Key.F3, (int)TerminalSurface.Help },
            { Key.F4, (int)TerminalSurface.Chat },
            { Key.F5, (int)TerminalSurface.Chat },
            { Key.C.WithCtrl, (int)TerminalSurface.Thoughts },
            { Key.Q.WithCtrl, (int)TerminalSurface.Activities }
        };

    [Theory]
    [InlineData((int)TerminalSurface.Chat)]
    [InlineData((int)TerminalSurface.Thoughts)]
    [InlineData((int)TerminalSurface.Activities)]
    [InlineData((int)TerminalSurface.Help)]
    public void F5_SaveActivitiesIsGlobalAndDoesNotChangeSurface(int surfaceValue)
    {
        TerminalSurface surface = (TerminalSurface)surfaceValue;
        int saves = 0;
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        using TerminalShellView shell = CreateShell(saveActivities: () => saves++);
        shell.Show(surface);
        shell.RegisterApplicationBindings(application);
        SessionToken shellSession = Assert.IsType<SessionToken>(application.Begin(shell));

        try
        {
            bool handled = application.Keyboard.RaiseKeyDownEvent(Key.F5);

            Assert.True(handled);
            Assert.Equal(1, saves);
            Assert.Equal(surface, shell.ActiveSurface);
        }
        finally
        {
            application.End(shellSession);
        }
    }

    private static TerminalShellView CreateShell(
        Func<TerminalSurface, View?>? resolveFocusTarget = null,
        Action<Exception>? reportNavigationFailure = null,
        Action? copy = null,
        Action? quit = null,
        Action? requestFullRefresh = null,
        Action<TerminalSurface>? activeSurfaceChanged = null,
        Action? saveActivities = null)
    {
        Dictionary<TerminalSurface, View> surfaces = new()
        {
            [TerminalSurface.Chat] = CreateSurface(),
            [TerminalSurface.Thoughts] = CreateSurface(),
            [TerminalSurface.Activities] = CreateSurface(),
            [TerminalSurface.Help] = CreateSurface()
        };

        return new TerminalShellView(
            new TerminalNavigationView(),
            surfaces,
            resolveFocusTarget ?? (surface => surfaces[surface]),
            copy ?? (() => { }),
            quit ?? (() => { }),
            requestFullRefresh ?? (() => { }),
            reportNavigationFailure ?? (_ => { }),
            activeSurfaceChanged,
            saveActivities);
    }

    private static View CreateSurface()
    {
        return new View
        {
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
    }

}
