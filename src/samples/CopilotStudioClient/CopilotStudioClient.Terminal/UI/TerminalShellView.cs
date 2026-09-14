#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

internal sealed class TerminalShellView : Runnable
{
    private readonly IReadOnlyDictionary<TerminalSurface, View> _surfaces;
    private readonly Func<TerminalSurface, View?> _resolveFocusTarget;
    private readonly Action<Exception> _reportNavigationFailure;

    internal TerminalShellView(
        TerminalNavigationView navigation,
        IReadOnlyDictionary<TerminalSurface, View> surfaces,
        Func<TerminalSurface, View?> resolveFocusTarget,
        Action copy,
        Action quit,
        Action<Exception> reportNavigationFailure)
    {
        Navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _surfaces = surfaces ?? throw new ArgumentNullException(nameof(surfaces));
        _resolveFocusTarget = resolveFocusTarget ?? throw new ArgumentNullException(nameof(resolveFocusTarget));
        ArgumentNullException.ThrowIfNull(copy);
        ArgumentNullException.ThrowIfNull(quit);
        _reportNavigationFailure = reportNavigationFailure
            ?? throw new ArgumentNullException(nameof(reportNavigationFailure));

        foreach (TerminalSurface surface in Enum.GetValues<TerminalSurface>())
        {
            if (!_surfaces.ContainsKey(surface))
            {
                throw new ArgumentException($"Missing view for {surface}.", nameof(surfaces));
            }
        }

        Title = string.Empty;
        BorderStyle = LineStyle.None;
        Width = Dim.Fill();
        Height = Dim.Fill();

        Navigation.X = 0;
        Navigation.Y = 0;
        Navigation.Width = Dim.Fill();
        Navigation.Height = 1;

        ContentRegion = new View
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true,
            BorderStyle = LineStyle.None
        };
        ContentRegion.Add(_surfaces.Values.Distinct().ToArray());
        Add(Navigation, ContentRegion);

        AddCommand(Command.Home, () => Navigate(TerminalSurface.Chat));
        AddCommand(Command.Find, () => Navigate(TerminalSurface.Thoughts));
        AddCommand(Command.Open, () => Navigate(TerminalSurface.Activities));
        AddCommand(Command.Context, () => Navigate(TerminalSurface.Help));
        AddCommand(
            Command.Cancel,
            () =>
            {
                if (ActiveSurface == TerminalSurface.Chat)
                {
                    return false;
                }

                ReturnToChat();
                return true;
            });
        AddCommand(
            Command.Copy,
            () =>
            {
                copy();
                return true;
            });
        AddCommand(
            Command.Quit,
            () =>
            {
                quit();
                return true;
            });

        SetVisibleSurface(TerminalSurface.Chat);
    }

    internal TerminalSurface ActiveSurface { get; private set; }

    internal TerminalNavigationView Navigation { get; }

    internal View ContentRegion { get; }

    internal void RegisterApplicationBindings(IApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);

        application.Keyboard.KeyBindings.AddApp(Key.F1, this, [Command.Home]);
        application.Keyboard.KeyBindings.AddApp(Key.F2, this, [Command.Find]);
        application.Keyboard.KeyBindings.AddApp(Key.F3, this, [Command.Open]);
        application.Keyboard.KeyBindings.AddApp(Key.F4, this, [Command.Context]);
        application.Keyboard.KeyBindings.Remove(Key.Esc);
        application.Keyboard.KeyBindings.AddApp(Key.Esc, this, [Command.Cancel]);
        application.Keyboard.KeyBindings.AddApp(Key.C.WithCtrl, this, [Command.Copy]);
        application.Keyboard.KeyBindings.AddApp(Key.Q.WithCtrl, this, [Command.Quit]);
    }

    internal void Show(TerminalSurface surface)
    {
        _ = Navigate(surface);
    }

    internal void ReturnToChat()
    {
        Show(TerminalSurface.Chat);
    }

    private bool Navigate(TerminalSurface surface)
    {
        try
        {
            SetVisibleSurface(surface);
            _resolveFocusTarget(surface)?.SetFocus();
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ObjectDisposedException)
        {
            _reportNavigationFailure(exception);
        }

        return true;
    }

    private void SetVisibleSurface(TerminalSurface surface)
    {
        View selectedSurface = _surfaces[surface];
        foreach (View view in _surfaces.Values.Distinct())
        {
            view.Visible = ReferenceEquals(view, selectedSurface);
        }

        ActiveSurface = surface;
        Navigation.ActiveSurface = surface;
        Navigation.SetNeedsDraw();
    }
}
