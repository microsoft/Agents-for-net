#nullable enable

using System.Text;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;

internal sealed class TerminalFooterView : View
{
    private readonly TerminalPalette _palette;

    internal TerminalFooterView(TerminalPalette palette)
        : this(
            palette,
            Console.OutputEncoding.CodePage == Encoding.UTF8.CodePage)
    {
    }

    internal TerminalFooterView(TerminalPalette palette, bool useUnicode)
    {
        _palette = palette ?? throw new ArgumentNullException(nameof(palette));
        Text = useUnicode
            ? "Enter send · F1–F4 views · Esc back · Ctrl+C copy · Ctrl+Q quit"
            : "Enter send - F1-F4 views - Esc back - Ctrl+C copy - Ctrl+Q quit";
        Role = TimelineRole.Muted;
        Height = 1;
        CanFocus = false;
    }

    internal new string Text { get; }

    internal TimelineRole Role { get; }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        int width = GetDrawWidth();
        int height = Math.Max(0, Viewport.Height > 0 ? Viewport.Height : Frame.Height);
        if (height == 0)
        {
            return true;
        }

        for (int row = 0; row < height; row++)
        {
            Move(0, row);
            SetAttribute(_palette.Get(Role));
            if (row == 0)
            {
                string text = Text.Length <= width ? Text : Text[..width];
                AddStr(text.PadRight(width));
            }
            else
            {
                AddStr(new string(' ', width));
            }
        }

        return true;
    }

    private int GetDrawWidth()
    {
        if (Viewport.Width > 0)
        {
            return Viewport.Width;
        }

        return Math.Max(1, Frame.Width);
    }
}
