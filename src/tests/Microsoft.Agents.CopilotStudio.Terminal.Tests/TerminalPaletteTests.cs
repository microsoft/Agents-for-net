#nullable enable

using System;
using Terminal.Gui.Drawing;

public sealed class TerminalPaletteTests
{
    [Fact]
    public void Create_UsesDarkTrueColorPaletteForDarkDetectedBackground()
    {
        TerminalPalette palette = TerminalPalette.Create(
            new Terminal.Gui.Drawing.Attribute(new Color("#c9d1d9"), new Color("#0d1117")),
            supportsTrueColor: true);

        Assert.True(palette.IsDark);
        Assert.True(palette.UsesTrueColor);
        Assert.NotEqual(palette.Get(TimelineRole.User), palette.Get(TimelineRole.Agent));
        Assert.NotEqual(palette.Get(TimelineRole.Thought), palette.Get(TimelineRole.Warning));
    }

    [Fact]
    public void Create_UsesLightTrueColorPaletteForLightDetectedBackground()
    {
        TerminalPalette palette = TerminalPalette.Create(
            new Terminal.Gui.Drawing.Attribute(new Color("#24292f"), new Color("#ffffff")),
            supportsTrueColor: true);

        Assert.False(palette.IsDark);
        Assert.NotEqual(palette.Get(TimelineRole.Primary), palette.Get(TimelineRole.Muted));
        Assert.NotEqual(palette.Get(TimelineRole.ActiveNavigation), palette.Get(TimelineRole.Link));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_UsesReadableSixteenColorFallbackWhenDetectionOrTrueColorIsUnavailable(
        bool hasDefault)
    {
        Terminal.Gui.Drawing.Attribute? terminalDefault = hasDefault
            ? new Terminal.Gui.Drawing.Attribute(ColorName16.White, ColorName16.Black)
            : null;

        TerminalPalette palette = TerminalPalette.Create(terminalDefault, supportsTrueColor: false);

        Assert.False(palette.UsesTrueColor);
        Assert.All(
            Enum.GetValues<TimelineRole>(),
            role => Assert.Contains(
                palette.Get(role).Foreground.GetClosestNamedColor16(),
                new[]
                {
                    ColorName16.Gray,
                    ColorName16.DarkGray,
                    ColorName16.Blue,
                    ColorName16.Green,
                    ColorName16.Magenta,
                    ColorName16.Cyan,
                    ColorName16.Yellow,
                    ColorName16.Red,
                    ColorName16.White,
                    ColorName16.Black
                }));
    }

    [Fact]
    public void Get_CombinesSemanticColorWithRequestedTextStyle()
    {
        TerminalPalette palette = TerminalPalette.Create(null, supportsTrueColor: false);

        Terminal.Gui.Drawing.Attribute value = palette.Get(
            TimelineRole.Link,
            TimelineTextStyle.Bold | TimelineTextStyle.Underline);

        Assert.True(value.Style.HasFlag(Terminal.Gui.Drawing.TextStyle.Bold));
        Assert.True(value.Style.HasFlag(Terminal.Gui.Drawing.TextStyle.Underline));
    }
}
