#nullable enable

using System;
using Terminal.Gui.Views;

[Collection("TerminalGui")]
public sealed class TerminalComposerViewTests
{
    [Theory]
    [InlineData(true, "╭", "╯")]
    [InlineData(false, "+", "+")]
    public void GetBorderRows_UsesRequestedBorderSet(bool unicode, string first, string last)
    {
        using TextField input = new();
        using TerminalComposerView view = new(input, LimitedPalette(), unicode)
        {
            Width = 20,
            Height = 3
        };

        view.Layout();
        string[] rows = view.GetBorderRows();

        Assert.StartsWith(first, rows[0], StringComparison.Ordinal);
        Assert.EndsWith(last, rows[2], StringComparison.Ordinal);
        Assert.Same(input, view.Input);
        Assert.Equal(new System.Drawing.Rectangle(2, 1, 16, 1), input.Frame);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 1, 0)]
    [InlineData(2, 2, 0)]
    [InlineData(3, 2, 0)]
    [InlineData(4, 2, 0)]
    public void Layout_ClampsInputFrameForTinyWidths(int width, int expectedX, int expectedWidth)
    {
        using TextField input = new();
        using TerminalComposerView view = new(input, LimitedPalette(), useUnicode: false)
        {
            Width = width,
            Height = 3
        };

        view.Layout();
        string[] rows = view.GetBorderRows();

        Assert.Equal(3, rows.Length);
        Assert.All(rows, row => Assert.True(row.Length >= 1));
        Assert.Equal(new System.Drawing.Rectangle(expectedX, 1, expectedWidth, 1), input.Frame);
    }

    [Fact]
    public void Footer_IsMutedAndCannotReceiveFocus()
    {
        using TerminalFooterView footer = new(LimitedPalette());

        Assert.False(footer.CanFocus);
        Assert.Contains("F1", footer.Text, StringComparison.Ordinal);
        Assert.Equal(TimelineRole.Muted, footer.Role);
    }

    private static TerminalPalette LimitedPalette() => TerminalPalette.Create(null, supportsTrueColor: false);
}
