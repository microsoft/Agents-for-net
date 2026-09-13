#nullable enable

using System.Collections.Generic;
using System.Text;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

internal sealed class TerminalTimelineView : View
{
    private static readonly TimelineLayoutResult EmptyLayout = new(
        Array.AsReadOnly(Array.Empty<TimelineLine>()),
        new Dictionary<string, TimelineRowRange>(StringComparer.Ordinal));
    private static readonly IReadOnlyList<TimelineLine> NoLines = Array.AsReadOnly(Array.Empty<TimelineLine>());

    private readonly TimelineScrollState _scrollState = new();
    private IReadOnlyList<ChatEntry> _entries = [];
    private TimelineLayoutResult _layout = EmptyLayout;
    private int _lastLayoutWidth = -1;

    internal TerminalTimelineView()
    {
        CanFocus = true;
    }

    internal bool CollapseCompletedThoughts { get; init; } = true;

    internal TimelineGlyphSet Glyphs { get; init; } = TimelineGlyphSet.Unicode;

    internal IReadOnlyList<TimelineLine> EmptyStateLines { get; init; } = NoLines;

    internal int ScrollOffset => _scrollState.Offset;

    internal int MaximumScrollOffset => _scrollState.MaximumOffset;

    internal IReadOnlyList<TimelineLine> RenderedLines
    {
        get
        {
            EnsureLayoutMatchesViewport();
            return _layout.Lines;
        }
    }

    internal void SetEntries(IReadOnlyList<ChatEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        _entries = entries.ToArray();
        RebuildLayout();
        SetNeedsDraw();
    }

    internal VisualRole GetVisualRole(TimelineRole role) => role switch
    {
        TimelineRole.Primary => VisualRole.Normal,
        TimelineRole.Muted => VisualRole.Disabled,
        TimelineRole.User => VisualRole.HotNormal,
        TimelineRole.Agent => VisualRole.Active,
        TimelineRole.Thought => VisualRole.HotNormal,
        TimelineRole.Link => VisualRole.Focus,
        TimelineRole.ActiveNavigation => VisualRole.HotFocus,
        TimelineRole.Warning => VisualRole.HotActive,
        TimelineRole.Error => VisualRole.HotActive,
        TimelineRole.Code => VisualRole.Code,
        _ => VisualRole.Normal
    };

    protected override void OnViewportChanged(DrawEventArgs args)
    {
        base.OnViewportChanged(args);
        EnsureLayoutMatchesViewport();
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        EnsureLayoutMatchesViewport();

        int visibleWidth = GetViewportWidth();
        int visibleHeight = GetViewportHeight();
        IReadOnlyList<TimelineLine> lines = _layout.Lines;
        int lineIndex = ScrollOffset;
        int visibleRow = 0;

        for (; visibleRow < visibleHeight && lineIndex < lines.Count; visibleRow++, lineIndex++)
        {
            ClearRow(visibleRow, visibleWidth);
            DrawLine(lines[lineIndex], visibleRow);
        }

        for (; visibleRow < visibleHeight; visibleRow++)
        {
            ClearRow(visibleRow, visibleWidth);
        }

        return true;
    }

    protected override bool OnKeyDown(Key key)
    {
        bool handled = key switch
        {
            _ when key == Key.CursorUp => ScrollBy(-1),
            _ when key == Key.CursorDown => ScrollBy(1),
            _ when key == Key.PageUp => ScrollBy(-Math.Max(1, GetViewportHeight())),
            _ when key == Key.PageDown => ScrollBy(Math.Max(1, GetViewportHeight())),
            _ when key == Key.Home => ScrollToStart(),
            _ when key == Key.End => ScrollToEnd(),
            _ => false
        };

        if (handled)
        {
            key.Handled = true;
            return true;
        }

        return base.OnKeyDown(key);
    }

    protected override bool OnMouseEvent(Mouse mouse)
    {
        bool handled;
        if ((mouse.Flags & MouseFlags.WheeledUp) == MouseFlags.WheeledUp)
        {
            handled = ScrollBy(-1);
        }
        else if ((mouse.Flags & MouseFlags.WheeledDown) == MouseFlags.WheeledDown)
        {
            handled = ScrollBy(1);
        }
        else
        {
            handled = false;
        }

        if (handled)
        {
            mouse.Handled = true;
            return true;
        }

        return base.OnMouseEvent(mouse);
    }

    private void EnsureLayoutMatchesViewport()
    {
        int width = GetLayoutWidth();
        if (width != _lastLayoutWidth)
        {
            RebuildLayout(width);
            return;
        }

        UpdateScrollDimensions();
    }

    private void RebuildLayout()
    {
        RebuildLayout(GetLayoutWidth());
    }

    private void RebuildLayout(int width)
    {
        int contentWidth = Math.Max(1, width);
        _layout = _entries.Count == 0
            ? new TimelineLayoutResult(BuildEmptyStateLayout(contentWidth), EmptyLayout.EntryRows)
            : TerminalTimelineLayout.Build(_entries, contentWidth, Glyphs, CollapseCompletedThoughts);
        _lastLayoutWidth = contentWidth;
        UpdateScrollDimensions();
    }

    private void UpdateScrollDimensions()
    {
        _scrollState.SetDimensions(_layout.Lines.Count, GetViewportHeight());
    }

    private int GetLayoutWidth()
    {
        if (Viewport.Width > 0)
        {
            return Viewport.Width;
        }

        if (Frame.Width > 0)
        {
            return Frame.Width;
        }

        return 1;
    }

    private int GetViewportWidth()
    {
        return Math.Max(1, Viewport.Width > 0 ? Viewport.Width : GetLayoutWidth());
    }

    private int GetViewportHeight()
    {
        if (Viewport.Height > 0)
        {
            return Viewport.Height;
        }

        return Math.Max(0, Frame.Height);
    }

    private void DrawLine(TimelineLine line, int visibleRow)
    {
        Move(0, visibleRow);
        foreach (TimelineSpan span in line.Spans)
        {
            SetAttribute(GetAttributeForRole(GetVisualRole(span.Role)));
            AddStr(span.Text);
        }
    }

    private void ClearRow(int visibleRow, int visibleWidth)
    {
        Move(0, visibleRow);
        SetAttribute(GetAttributeForRole(GetVisualRole(TimelineRole.Primary)));
        AddStr(new string(' ', visibleWidth));
    }

    private IReadOnlyList<TimelineLine> BuildEmptyStateLayout(int width)
    {
        if (EmptyStateLines.Count == 0)
        {
            return NoLines;
        }

        List<TimelineLine> wrappedLines = new(EmptyStateLines.Count);
        foreach (TimelineLine line in EmptyStateLines)
        {
            if (line.Spans.Count == 0)
            {
                wrappedLines.Add(line);
                continue;
            }

            StringBuilder text = new();
            foreach (TimelineSpan span in line.Spans)
            {
                text.Append(span.Text);
            }

            TimelineRole role = line.Spans[0].Role;
            foreach (string wrappedText in TerminalTimelineLayout.WrapText(text.ToString(), width))
            {
                wrappedLines.Add(new TimelineLine(line.EntryKey, [new TimelineSpan(wrappedText, role)]));
            }
        }

        return wrappedLines.Count == 0
            ? NoLines
            : Array.AsReadOnly(wrappedLines.ToArray());
    }

    private bool ScrollBy(int delta)
    {
        _scrollState.ScrollBy(delta);
        SetNeedsDraw();
        return true;
    }

    private bool ScrollToStart()
    {
        _scrollState.ScrollToStart();
        SetNeedsDraw();
        return true;
    }

    private bool ScrollToEnd()
    {
        _scrollState.ScrollToEnd();
        SetNeedsDraw();
        return true;
    }
}
