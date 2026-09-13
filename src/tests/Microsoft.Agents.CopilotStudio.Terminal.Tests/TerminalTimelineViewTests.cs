#nullable enable

using System;
using System.Linq;
using System.Collections.Generic;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

[Collection("TerminalGui")]
public sealed class TerminalTimelineViewTests
{
    [Fact]
    public void SetEntries_ReplacesRenderedStreamingEntryByKey()
    {
        using TerminalTimelineView view = new() { Width = 40, Height = 10 };

        view.SetEntries([Entry("stream", "Hel", isTransient: true)]);
        view.SetEntries([Entry("stream", "Hello", isTransient: true)]);

        Assert.Single(
            view.RenderedLines,
            line => line.EntryKey == "stream" && PlainText(line) == "Hello");
        Assert.DoesNotContain(view.RenderedLines, line => PlainText(line) == "Hel");
    }

    [Fact]
    public void Layout_RewrapsWhenViewportNarrows()
    {
        using TerminalTimelineView view = new() { Width = 30, Height = 10 };

        view.SetEntries([Entry("a", "one two three four")]);
        int wideLineCount = view.RenderedLines.Count;

        view.Width = 10;
        view.Layout();

        Assert.True(view.RenderedLines.Count > wideLineCount);
    }

    [Fact]
    public void EmptyState_RewrapsWhenViewportNarrows()
    {
        using TerminalTimelineView view = new()
        {
            Width = 60,
            Height = 4,
            EmptyStateLines =
            [
                new TimelineLine(
                    string.Empty,
                    [new TimelineSpan("Connected conversations and streaming activity appear here.", TimelineRole.Muted)])
            ]
        };

        int wideLineCount = view.RenderedLines.Count;

        view.Width = 12;
        view.Layout();

        Assert.True(view.RenderedLines.Count > wideLineCount);
        Assert.All(view.RenderedLines, line => Assert.True(PlainText(line).Length <= 12));
    }

    [Fact]
    public void Drawing_UsesPaletteAndTextStyleForEachSpan()
    {
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        TerminalPalette palette = TerminalPalette.Create(
            new Terminal.Gui.Drawing.Attribute(new Color("#c9d1d9"), new Color("#0d1117")),
            supportsTrueColor: true);
        using TerminalTimelineView view = new()
        {
            X = 0,
            Y = 0,
            Width = 40,
            Height = 4,
            Palette = palette
        };
        using Window host = new()
        {
            Width = 40,
            Height = 4
        };
        host.Add(view);

        view.SetEntries([Entry("a", "**bold** and `code`")]);
        RunOneIteration(application, host);
        IDriver driver = Assert.IsAssignableFrom<IDriver>(application.Driver);
        Assert.NotNull(driver.Contents);
        Cell[,] contents = driver.Contents!;

        Assert.Contains(view.RenderedLines.SelectMany(line => line.Spans), span =>
            span.Text == "bold" && span.Style.HasFlag(TimelineTextStyle.Bold));
        Assert.Contains(view.RenderedLines.SelectMany(line => line.Spans), span =>
            span.Text == "code" && span.Role == TimelineRole.Code);
        Assert.Equal(palette.Get(TimelineRole.Agent, TimelineTextStyle.Bold), FindAttribute(contents, "b"));
        Assert.Equal(palette.Get(TimelineRole.Code), FindAttribute(contents, "c"));
    }

    [Fact]
    public void KeyboardScrolling_PreservesManualPositionAcrossUpdate()
    {
        using TerminalTimelineView view = new() { Width = 20, Height = 4 };

        view.SetEntries(ManyEntries(10));
        view.NewKeyDownEvent(Key.End);
        view.NewKeyDownEvent(Key.CursorUp);
        int manualOffset = view.ScrollOffset;

        view.SetEntries(ManyEntries(11));

        Assert.Equal(manualOffset, view.ScrollOffset);
    }

    [Fact]
    public void UpdatingLastStreamKeepsLatestLineVisibleWhenFollowing()
    {
        using TerminalTimelineView view = new() { Width = 12, Height = 4 };

        view.SetEntries(ManyEntries(8));
        view.NewKeyDownEvent(Key.End);

        view.SetEntries(ManyEntries(8, finalText: "long streaming update wraps"));

        Assert.Equal(view.MaximumScrollOffset, view.ScrollOffset);
    }

    [Fact]
    public void ResizeToTallerViewport_WhenClampReachesBottom_FollowsAppendedEntries()
    {
        using TerminalTimelineView view = new() { Width = 20, Height = 4 };

        view.SetEntries(ManyEntries(4));
        view.NewKeyDownEvent(Key.End);
        view.NewKeyDownEvent(Key.CursorUp);

        view.Height = 5;
        view.Layout();

        Assert.Equal(view.MaximumScrollOffset, view.ScrollOffset);

        view.SetEntries(ManyEntries(5));

        Assert.Equal(view.MaximumScrollOffset, view.ScrollOffset);
    }

    [Fact]
    public void MouseWheelScrolling_MovesOneRowPerWheelEvent()
    {
        using TerminalTimelineView view = new() { Width = 20, Height = 4 };

        view.SetEntries(ManyEntries(10));
        int bottomOffset = view.ScrollOffset;

        Assert.True(view.NewMouseEvent(new Mouse { Flags = MouseFlags.WheeledUp }) ?? false);
        Assert.Equal(bottomOffset - 1, view.ScrollOffset);

        Assert.True(view.NewMouseEvent(new Mouse { Flags = MouseFlags.WheeledDown }) ?? false);
        Assert.Equal(bottomOffset, view.ScrollOffset);
    }

    [Fact]
    public void MouseWheelScrolling_HandlesWheelFlagsCombinedWithModifiers()
    {
        using TerminalTimelineView view = new() { Width = 20, Height = 4 };

        view.SetEntries(ManyEntries(10));
        int bottomOffset = view.ScrollOffset;

        Assert.True(view.NewMouseEvent(new Mouse { Flags = MouseFlags.WheeledUp | MouseFlags.Shift }) ?? false);
        Assert.Equal(bottomOffset - 1, view.ScrollOffset);

        Assert.True(view.NewMouseEvent(new Mouse { Flags = MouseFlags.WheeledDown | MouseFlags.Ctrl }) ?? false);
        Assert.Equal(bottomOffset, view.ScrollOffset);
    }

    [Fact]
    public void RenderedLines_DoesNotAllowMutationThroughCollectionCast()
    {
        using TerminalTimelineView view = new() { Width = 40, Height = 10 };

        view.SetEntries([Entry("entry", "Hello")]);

        ICollection<TimelineLine> renderedLines = Assert.IsAssignableFrom<ICollection<TimelineLine>>(view.RenderedLines);

        Assert.Throws<NotSupportedException>(() => renderedLines.Add(new TimelineLine("mutated", [])));
        Assert.DoesNotContain(view.RenderedLines, line => line.EntryKey == "mutated");
    }

    [Fact]
    public void RenderedLines_DoesNotAllowElementReplacementThroughListIndexer()
    {
        using TerminalTimelineView view = new() { Width = 40, Height = 10 };

        view.SetEntries([Entry("entry", "Hello")]);

        IList<TimelineLine> renderedLines = Assert.IsAssignableFrom<IList<TimelineLine>>(view.RenderedLines);

        Assert.Throws<NotSupportedException>(() => renderedLines[0] = new TimelineLine("mutated", []));
        Assert.Equal("entry", view.RenderedLines[0].EntryKey);
    }

    private static ChatEntry Entry(string key, string text, bool isTransient = false)
    {
        return new ChatEntry(key, ChatEntryKind.Agent, "Agent", text, isTransient, [], [], key);
    }

    private static ChatEntry[] ManyEntries(int count, string? finalText = null)
    {
        return Enumerable.Range(0, count)
            .Select(index => new ChatEntry(
                $"entry-{index}",
                ChatEntryKind.Agent,
                "Agent",
                index == count - 1 ? finalText ?? $"item {index}" : $"item {index}",
                index == count - 1,
                [],
                [],
                $"entry-{index}"))
            .ToArray();
    }

    private static string PlainText(TimelineLine line)
    {
        return string.Concat(line.Spans.Select(span => span.Text));
    }

    private static Terminal.Gui.Drawing.Attribute? FindAttribute(Cell[,] contents, string grapheme)
    {
        for (int row = 0; row < contents.GetLength(0); row++)
        {
            for (int column = 0; column < contents.GetLength(1); column++)
            {
                if (contents[row, column].Grapheme == grapheme)
                {
                    return contents[row, column].Attribute;
                }
            }
        }

        throw new InvalidOperationException($"Rendered cell '{grapheme}' was not found.");
    }

    private static void RunOneIteration(IApplication application, Window window)
    {
        application.StopAfterFirstIteration = true;
        application.Run(window);
    }
}
