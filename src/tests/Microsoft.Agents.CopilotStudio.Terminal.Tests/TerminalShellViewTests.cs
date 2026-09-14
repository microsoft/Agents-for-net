#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
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
        using TerminalShellView shell = CreateShell();

        bool? handled = shell.InvokeCommand(Command.Cancel);

        Assert.False(handled);
        Assert.Equal(TerminalSurface.Chat, shell.ActiveSurface);
    }

    private static TerminalShellView CreateShell(
        Func<TerminalSurface, View?>? resolveFocusTarget = null,
        Action<Exception>? reportNavigationFailure = null)
    {
        Dictionary<TerminalSurface, View> surfaces = new()
        {
            [TerminalSurface.Chat] = new View(),
            [TerminalSurface.Thoughts] = new View(),
            [TerminalSurface.Activities] = new View(),
            [TerminalSurface.Help] = new View()
        };

        return new TerminalShellView(
            new TerminalNavigationView(),
            surfaces,
            resolveFocusTarget ?? (surface => surfaces[surface]),
            copy: () => { },
            quit: () => { },
            reportNavigationFailure ?? (_ => { }));
    }
}
