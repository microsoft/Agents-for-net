#nullable enable

using System;
using System.Linq;
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
        Assert.Equal(new Color("#f2cc60"), palette.Get(TimelineRole.User).Foreground);
        Assert.True(ContrastRatio(
            palette.Get(TimelineRole.User).Foreground,
            palette.Get(TimelineRole.User).Background) >= 4.5);
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
        Assert.Equal(new Color("#0969da"), palette.Get(TimelineRole.User).Foreground);
        Assert.True(ContrastRatio(
            palette.Get(TimelineRole.User).Foreground,
            palette.Get(TimelineRole.User).Background) >= 4.5);
        Assert.NotEqual(palette.Get(TimelineRole.Primary), palette.Get(TimelineRole.Muted));
        Assert.NotEqual(palette.Get(TimelineRole.ActiveNavigation), palette.Get(TimelineRole.Link));
    }

    [Theory]
    [InlineData(ColorName16.White, ColorName16.Black, ColorName16.Yellow)]
    [InlineData(ColorName16.Black, ColorName16.White, ColorName16.Blue)]
    public void Create_UsesBackgroundAwareUserColorInLimitedColorPalettes(
        ColorName16 foreground,
        ColorName16 background,
        ColorName16 expectedUserColor)
    {
        TerminalPalette palette = TerminalPalette.Create(
            new Terminal.Gui.Drawing.Attribute(foreground, background),
            supportsTrueColor: false);

        Assert.Equal(expectedUserColor, palette.Get(TimelineRole.User).Foreground);
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

    [Fact]
    public void TimelineTextStyle_DeclaresFlagsSemantics()
    {
        Assert.True(typeof(TimelineTextStyle).IsDefined(typeof(FlagsAttribute), inherit: false));
        Assert.Equal(
            "Bold, Underline",
            (TimelineTextStyle.Bold | TimelineTextStyle.Underline).ToString());
    }

    [Theory]
    [InlineData("#c9d1d9", "#0d1117", "#161b22")]
    [InlineData("#24292f", "#ffffff", "#f6f8fa")]
    public void Create_TrueColorCodeRoleUsesElevatedContrastingBackgroundOnly(
        string foreground,
        string background,
        string expectedCodeBackground)
    {
        Terminal.Gui.Drawing.Attribute terminalDefault =
            new(new Color(foreground), new Color(background));
        TerminalPalette palette = TerminalPalette.Create(terminalDefault, supportsTrueColor: true);

        Terminal.Gui.Drawing.Attribute code = palette.Get(TimelineRole.Code);

        Assert.Equal(new Color(expectedCodeBackground), code.Background);
        Assert.NotEqual(terminalDefault.Background, code.Background);
        Assert.True(ContrastRatio(code.Foreground, code.Background) >= 4.5);
        Assert.All(
            Enum.GetValues<TimelineRole>().Where(role => role != TimelineRole.Code),
            role => Assert.Equal(terminalDefault.Background, palette.Get(role).Background));
    }

    [Theory]
    [InlineData(ColorName16.White, ColorName16.Black, ColorName16.DarkGray)]
    [InlineData(ColorName16.Black, ColorName16.White, ColorName16.Gray)]
    public void Create_LimitedColorCodeRoleUsesElevatedContrastingBackgroundOnly(
        ColorName16 foreground,
        ColorName16 background,
        ColorName16 expectedCodeBackground)
    {
        Terminal.Gui.Drawing.Attribute terminalDefault = new(foreground, background);
        TerminalPalette palette = TerminalPalette.Create(terminalDefault, supportsTrueColor: false);

        Terminal.Gui.Drawing.Attribute code = palette.Get(TimelineRole.Code);

        Assert.Equal(new Color(expectedCodeBackground), code.Background);
        Assert.NotEqual(terminalDefault.Background, code.Background);
        Assert.True(ContrastRatio(code.Foreground, code.Background) >= 4.5);
        Assert.All(
            Enum.GetValues<TimelineRole>().Where(role => role != TimelineRole.Code),
            role => Assert.Equal(terminalDefault.Background, palette.Get(role).Background));
    }

    private static double ContrastRatio(Color first, Color second)
    {
        double firstLuminance = RelativeLuminance(first);
        double secondLuminance = RelativeLuminance(second);
        double lighter = Math.Max(firstLuminance, secondLuminance);
        double darker = Math.Min(firstLuminance, secondLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(Color color)
    {
        return (0.2126 * Linearize(color.R))
            + (0.7152 * Linearize(color.G))
            + (0.0722 * Linearize(color.B));
    }

    private static double Linearize(byte channel)
    {
        double value = channel / 255d;
        return value <= 0.04045
            ? value / 12.92
            : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
