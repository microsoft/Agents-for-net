#nullable enable

using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

internal sealed class TerminalComposerView : View
{
    private readonly TerminalPalette _palette;
    private readonly BorderCharacters _borderCharacters;

    internal TerminalComposerView(
        TextField input,
        TerminalPalette palette,
        bool useUnicode)
    {
        Input = input ?? throw new ArgumentNullException(nameof(input));
        _palette = palette ?? throw new ArgumentNullException(nameof(palette));
        _borderCharacters = useUnicode
            ? new BorderCharacters('╭', '─', '╮', '│', '╰', '╯')
            : new BorderCharacters('+', '-', '+', '|', '+', '+');
        Height = 3;
        CanFocus = true;
        TabStop = TabBehavior.NoStop;
        Add(Input);
    }

    internal TextField Input { get; }

    internal string[] GetBorderRows()
    {
        int width = GetDrawWidth();
        return
        [
            ComposeHorizontalRow(_borderCharacters.TopLeft, _borderCharacters.Horizontal, _borderCharacters.TopRight, width),
            ComposeMiddleRow(width),
            ComposeHorizontalRow(_borderCharacters.BottomLeft, _borderCharacters.Horizontal, _borderCharacters.BottomRight, width)
        ];
    }

    protected override void OnSubViewLayout(LayoutEventArgs args)
    {
        base.OnSubViewLayout(args);

        int width = GetLayoutWidth();
        int height = Math.Max(0, Viewport.Height > 0 ? Viewport.Height : Frame.Height);
        int inputX = width == 0 ? 0 : Math.Min(2, width);
        int inputY = height == 0 ? 0 : Math.Min(1, height - 1);
        int inputWidth = Math.Max(0, width - 4);
        int inputHeight = Math.Max(0, Math.Min(1, height - inputY));

        Input.Frame = new System.Drawing.Rectangle(inputX, inputY, inputWidth, inputHeight);
        Input.Layout();
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        int width = GetDrawWidth();
        int height = Math.Max(0, Viewport.Height > 0 ? Viewport.Height : Frame.Height);
        if (height == 0)
        {
            return true;
        }

        string[] rows = GetBorderRows();
        int rowsToDraw = Math.Min(height, rows.Length);
        for (int row = 0; row < rowsToDraw; row++)
        {
            Move(0, row);
            SetAttribute(_palette.Get(TimelineRole.Muted));
            AddStr(rows[row]);
        }

        for (int row = rowsToDraw; row < height; row++)
        {
            Move(0, row);
            SetAttribute(_palette.Get(TimelineRole.Primary));
            AddStr(new string(' ', width));
        }

        if (height > 1 && width > 1)
        {
            Move(1, 1);
            SetAttribute(_palette.Get(TimelineRole.User));
            AddStr(">");
        }

        return true;
    }

    private string ComposeMiddleRow(int width)
    {
        if (width <= 1)
        {
            return _borderCharacters.Vertical.ToString();
        }

        if (width == 2)
        {
            return string.Concat(_borderCharacters.Vertical, _borderCharacters.Vertical);
        }

        return string.Concat(
            _borderCharacters.Vertical,
            new string(' ', width - 2),
            _borderCharacters.Vertical);
    }

    private static string ComposeHorizontalRow(char left, char horizontal, char right, int width)
    {
        if (width <= 1)
        {
            return left.ToString();
        }

        if (width == 2)
        {
            return string.Concat(left, right);
        }

        return string.Concat(left, new string(horizontal, width - 2), right);
    }

    private int GetDrawWidth()
    {
        if (Viewport.Width > 0)
        {
            return Viewport.Width;
        }

        return Math.Max(1, Frame.Width);
    }

    private int GetLayoutWidth()
    {
        if (Viewport.Width > 0)
        {
            return Viewport.Width;
        }

        return Math.Max(0, Frame.Width);
    }

    private readonly record struct BorderCharacters(
        char TopLeft,
        char Horizontal,
        char TopRight,
        char Vertical,
        char BottomLeft,
        char BottomRight);
}
