#nullable enable

using System;
using System.Collections.Generic;
using GuiAttribute = Terminal.Gui.Drawing.Attribute;
using Terminal.Gui.Drawing;

internal enum TimelineTextStyle
{
    None = 0,
    Bold = 1,
    Italic = 2,
    Underline = 4
}

internal sealed class TerminalPalette
{
    private static readonly IReadOnlyDictionary<TimelineRole, Color> DarkTrueColorForegrounds =
        new Dictionary<TimelineRole, Color>
        {
            [TimelineRole.Primary] = new("#c9d1d9"),
            [TimelineRole.Muted] = new("#8b949e"),
            [TimelineRole.User] = new("#58a6ff"),
            [TimelineRole.Agent] = new("#3fb950"),
            [TimelineRole.Thought] = new("#bc8cff"),
            [TimelineRole.Link] = new("#58a6ff"),
            [TimelineRole.ActiveNavigation] = new("#f78166"),
            [TimelineRole.Warning] = new("#d29922"),
            [TimelineRole.Error] = new("#f85149"),
            [TimelineRole.Code] = new("#e6edf3")
        };

    private static readonly IReadOnlyDictionary<TimelineRole, Color> LightTrueColorForegrounds =
        new Dictionary<TimelineRole, Color>
        {
            [TimelineRole.Primary] = new("#24292f"),
            [TimelineRole.Muted] = new("#57606a"),
            [TimelineRole.User] = new("#0969da"),
            [TimelineRole.Agent] = new("#1a7f37"),
            [TimelineRole.Thought] = new("#8250df"),
            [TimelineRole.Link] = new("#0969da"),
            [TimelineRole.ActiveNavigation] = new("#cf222e"),
            [TimelineRole.Warning] = new("#9a6700"),
            [TimelineRole.Error] = new("#cf222e"),
            [TimelineRole.Code] = new("#24292f")
        };

    private static readonly IReadOnlyDictionary<TimelineRole, ColorName16> DarkFallbackForegrounds =
        new Dictionary<TimelineRole, ColorName16>
        {
            [TimelineRole.Primary] = ColorName16.White,
            [TimelineRole.Muted] = ColorName16.Gray,
            [TimelineRole.User] = ColorName16.Blue,
            [TimelineRole.Agent] = ColorName16.Green,
            [TimelineRole.Thought] = ColorName16.Magenta,
            [TimelineRole.Link] = ColorName16.Blue,
            [TimelineRole.ActiveNavigation] = ColorName16.Cyan,
            [TimelineRole.Warning] = ColorName16.Yellow,
            [TimelineRole.Error] = ColorName16.Red,
            [TimelineRole.Code] = ColorName16.White
        };

    private static readonly IReadOnlyDictionary<TimelineRole, ColorName16> LightFallbackForegrounds =
        new Dictionary<TimelineRole, ColorName16>
        {
            [TimelineRole.Primary] = ColorName16.Black,
            [TimelineRole.Muted] = ColorName16.DarkGray,
            [TimelineRole.User] = ColorName16.Blue,
            [TimelineRole.Agent] = ColorName16.Green,
            [TimelineRole.Thought] = ColorName16.Magenta,
            [TimelineRole.Link] = ColorName16.Blue,
            [TimelineRole.ActiveNavigation] = ColorName16.Red,
            [TimelineRole.Warning] = ColorName16.Yellow,
            [TimelineRole.Error] = ColorName16.Red,
            [TimelineRole.Code] = ColorName16.Black
        };

    private readonly GuiAttribute _terminalDefault;
    private readonly IReadOnlyDictionary<TimelineRole, Color> _trueColorForegrounds;
    private readonly IReadOnlyDictionary<TimelineRole, ColorName16> _fallbackForegrounds;

    private TerminalPalette(
        GuiAttribute terminalDefault,
        bool isDark,
        bool usesTrueColor,
        IReadOnlyDictionary<TimelineRole, Color> trueColorForegrounds,
        IReadOnlyDictionary<TimelineRole, ColorName16> fallbackForegrounds)
    {
        _terminalDefault = terminalDefault;
        IsDark = isDark;
        UsesTrueColor = usesTrueColor;
        _trueColorForegrounds = trueColorForegrounds;
        _fallbackForegrounds = fallbackForegrounds;
    }

    internal bool IsDark { get; }

    internal bool UsesTrueColor { get; }

    internal static TerminalPalette Create(
        GuiAttribute? terminalDefault,
        bool supportsTrueColor)
    {
        GuiAttribute resolvedDefault = terminalDefault ?? GuiAttribute.Default;
        bool isDark = resolvedDefault.Background.IsDarkColor();
        bool usesTrueColor = terminalDefault is not null && supportsTrueColor;

        return new TerminalPalette(
            resolvedDefault,
            isDark,
            usesTrueColor,
            isDark ? DarkTrueColorForegrounds : LightTrueColorForegrounds,
            isDark ? DarkFallbackForegrounds : LightFallbackForegrounds);
    }

    internal GuiAttribute Get(
        TimelineRole role,
        TimelineTextStyle style = TimelineTextStyle.None)
    {
        TextStyle textStyle = ToTerminalTextStyle(style);
        if (UsesTrueColor)
        {
            return new GuiAttribute(_trueColorForegrounds[role], _terminalDefault.Background, textStyle);
        }

        return new GuiAttribute(_fallbackForegrounds[role], _terminalDefault.Background, textStyle);
    }

    internal Scheme CreateControlScheme()
    {
        Scheme scheme = new()
        {
            Normal = Get(TimelineRole.Primary),
            Focus = Get(TimelineRole.Link, TimelineTextStyle.Underline),
            Active = Get(TimelineRole.ActiveNavigation),
            HotNormal = Get(TimelineRole.Link),
            HotFocus = Get(TimelineRole.ActiveNavigation, TimelineTextStyle.Underline),
            HotActive = Get(TimelineRole.Warning),
            Highlight = Get(TimelineRole.ActiveNavigation, TimelineTextStyle.Underline),
            Editable = Get(TimelineRole.Primary),
            ReadOnly = Get(TimelineRole.Muted),
            Disabled = Get(TimelineRole.Muted),
            Code = Get(TimelineRole.Code)
        };

        return scheme;
    }

    private static TextStyle ToTerminalTextStyle(TimelineTextStyle style)
    {
        TextStyle textStyle = TextStyle.None;

        if ((style & TimelineTextStyle.Bold) != 0)
        {
            textStyle |= TextStyle.Bold;
        }

        if ((style & TimelineTextStyle.Italic) != 0)
        {
            textStyle |= TextStyle.Italic;
        }

        if ((style & TimelineTextStyle.Underline) != 0)
        {
            textStyle |= TextStyle.Underline;
        }

        return textStyle;
    }
}
