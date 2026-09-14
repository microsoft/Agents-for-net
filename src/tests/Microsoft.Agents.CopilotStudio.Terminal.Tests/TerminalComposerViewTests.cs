#nullable enable

using System;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.ViewBase;
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
    public void InitializedRootFocusesEmbeddedInputThroughComposerHierarchy()
    {
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        using TextField input = new();
        using TerminalComposerView composer = new(input, LimitedPalette(), useUnicode: false)
        {
            Width = 20,
            Height = 3
        };
        using View root = new()
        {
            Width = 20,
            Height = 3,
            CanFocus = true
        };
        using Window window = new()
        {
            Width = 20,
            Height = 3
        };

        root.Add(composer);
        window.Add(root);

        RunOneIteration(application, window);

        Assert.True(input.HasFocus);
        Assert.Same(input, root.MostFocused);
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

    private static void RunOneIteration(IApplication application, Window window)
    {
        application.StopAfterFirstIteration = true;
        application.Run(window);
    }
}
