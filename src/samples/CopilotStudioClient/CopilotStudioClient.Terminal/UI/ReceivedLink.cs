#nullable enable

using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

internal sealed class ReceivedLink : Link
{
    private readonly Action<Uri> _requestOpen;

    internal ReceivedLink(string text, Uri target, Action<Uri> requestOpen)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(requestOpen);

        Text = text;
        Target = target;
        _requestOpen = requestOpen;
    }

    internal Uri Target { get; }

    protected override bool OnAccepting(CommandEventArgs args)
    {
        _requestOpen(Target);
        return true;
    }

    protected override void OnActivated(ICommandContext? context)
    {
        _requestOpen(Target);
    }

    protected override bool OnUrlChanging(ValueChangingEventArgs<string> args)
    {
        return true;
    }
}
