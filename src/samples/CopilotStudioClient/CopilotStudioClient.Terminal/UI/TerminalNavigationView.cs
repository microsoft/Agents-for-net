#nullable enable

using System.Collections.Generic;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;

internal enum TerminalSurface
{
    Chat,
    Thoughts,
    Activities,
    Help
}

internal sealed class TerminalNavigationView : View
{
    private static readonly (TerminalSurface Surface, string Shortcut, string Label)[] Surfaces =
    [
        (TerminalSurface.Chat, "F1", "Chat"),
        (TerminalSurface.Thoughts, "F2", "Thoughts"),
        (TerminalSurface.Activities, "F3", "Activities"),
        (TerminalSurface.Help, "F4", "Help")
    ];

    internal TerminalNavigationView()
    {
        Height = 1;
        CanFocus = false;
    }

    internal TerminalPalette Palette { get; init; } = TerminalPalette.Create(null, supportsTrueColor: false);

    internal TerminalSurface ActiveSurface { get; set; }

    internal IReadOnlyList<TimelineSpan> GetSpans()
    {
        List<TimelineSpan> spans = new(Surfaces.Length * 4 - 1);
        for (int index = 0; index < Surfaces.Length; index++)
        {
            if (index > 0)
            {
                spans.Add(new TimelineSpan("   ", TimelineRole.Muted));
            }

            (TerminalSurface surface, string shortcut, string label) = Surfaces[index];
            if (surface == ActiveSurface)
            {
                spans.Add(new TimelineSpan(
                    $"{shortcut} {label}",
                    TimelineRole.ActiveNavigation,
                    TimelineTextStyle.Underline));
                continue;
            }

            spans.Add(new TimelineSpan(shortcut, TimelineRole.Link));
            spans.Add(new TimelineSpan(" ", TimelineRole.Muted));
            spans.Add(new TimelineSpan(label, TimelineRole.Muted));
        }

        return Array.AsReadOnly(spans.ToArray());
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        int width = GetDrawWidth();
        int height = GetDrawHeight();
        if (height == 0)
        {
            return true;
        }

        for (int row = 0; row < height; row++)
        {
            Move(0, row);
            SetAttribute(Palette.Get(TimelineRole.Primary));
            AddStr(new string(' ', width));
        }

        int remaining = width;
        Move(0, 0);
        foreach (TimelineSpan span in GetSpans())
        {
            if (remaining <= 0)
            {
                break;
            }

            string text = span.Text.Length <= remaining ? span.Text : span.Text[..remaining];
            SetAttribute(Palette.Get(span.Role, span.Style));
            AddStr(text);
            remaining -= text.Length;
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

    private int GetDrawHeight()
    {
        if (Viewport.Height > 0)
        {
            return Viewport.Height;
        }

        return Math.Max(0, Frame.Height);
    }
}
