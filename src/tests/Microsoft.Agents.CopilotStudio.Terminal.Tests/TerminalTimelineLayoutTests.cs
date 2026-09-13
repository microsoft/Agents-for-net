#nullable enable

using System;
using System.Linq;
using System.Text;
using Terminal.Gui.Text;

public sealed class TerminalTimelineLayoutTests
{
    [Fact]
    public void Build_AssignsSemanticGlyphsAndRoles()
    {
        ChatEntry[] entries =
        [
            Entry("u", ChatEntryKind.User, "You", "Question"),
            Entry("a", ChatEntryKind.Agent, "Agent", "Answer"),
            Entry("s", ChatEntryKind.Status, "Status", "Working", isTransient: true),
            Entry("e", ChatEntryKind.Event, "Event", "Tool finished"),
            Entry("x", ChatEntryKind.Diagnostic, "Error", "Failed")
        ];

        TimelineLayoutResult result = TerminalTimelineLayout.Build(
            entries,
            width: 40,
            TimelineGlyphSet.Unicode,
            collapseCompletedThoughts: true);

        Assert.Equal(
            [">  You", "Question", "", "●  Agent", "Answer", "", "○  Status", "Working", "", "↗  Event", "Tool finished", "", "!  Error", "Failed"],
            result.Lines.Select(PlainText));
        Assert.Equal(TimelineRole.Accent, result.Lines[0].Spans[0].Role);
        Assert.Equal(TimelineRole.Success, result.Lines[3].Spans[0].Role);
        Assert.Equal(TimelineRole.Muted, result.Lines[6].Spans[0].Role);
        Assert.Equal(TimelineRole.Warning, result.Lines[12].Spans[0].Role);
    }

    [Fact]
    public void Build_UsesAsciiGlyphSetWhenRequested()
    {
        TimelineLayoutResult result = TerminalTimelineLayout.Build(
            [Entry("a", ChatEntryKind.Agent, "Agent", "Answer")],
            width: 40,
            TimelineGlyphSet.Ascii,
            collapseCompletedThoughts: true);

        Assert.Equal("*  Agent", PlainText(result.Lines[0]));
    }

    [Fact]
    public void ForEncoding_UsesAsciiFallbackForNonUnicodeOutput()
    {
        Assert.Equal(TimelineGlyphSet.Ascii, TimelineGlyphSet.ForEncoding(Encoding.ASCII));
        Assert.Equal(TimelineGlyphSet.Unicode, TimelineGlyphSet.ForEncoding(Encoding.UTF8));
    }

    [Fact]
    public void Build_SanitizesControlsAndWrapsBodyToWidth()
    {
        TimelineLayoutResult result = TerminalTimelineLayout.Build(
            [Entry("a", ChatEntryKind.Agent, "Agent", "alpha beta\u001B gamma")],
            width: 12,
            TimelineGlyphSet.Ascii,
            collapseCompletedThoughts: true);

        Assert.Equal(
            ["*  Agent", "alpha beta", "\\u001B", "gamma"],
            result.Lines.Select(PlainText));
        Assert.All(result.Lines, line => Assert.True(PlainText(line).Length <= 12));
    }

    [Fact]
    public void Build_CollapsesCompletedThoughtButKeepsActiveThoughtExpanded()
    {
        TimelineLayoutResult result = TerminalTimelineLayout.Build(
            [
                Entry("active", ChatEntryKind.Thought, "Reasoning", "Checking account", isTransient: true),
                Entry("done", ChatEntryKind.Thought, "Reasoning", "Compared all records", isTransient: false)
            ],
            width: 80,
            TimelineGlyphSet.Unicode,
            collapseCompletedThoughts: true);

        Assert.Contains(result.Lines, line => PlainText(line) == "Checking account");
        Assert.DoesNotContain(result.Lines, line => PlainText(line) == "Compared all records");
        Assert.Contains(
            result.Lines,
            line => PlainText(line) == "Reasoning complete · Ctrl+2 for details");
    }

    [Fact]
    public void Build_CollapsedThoughtSummaryUsesAsciiOnlyWithAsciiGlyphs()
    {
        TimelineLayoutResult result = TerminalTimelineLayout.Build(
            [Entry("done", ChatEntryKind.Thought, "Reasoning", "Compared all records")],
            width: 80,
            TimelineGlyphSet.Ascii,
            collapseCompletedThoughts: true);

        string summary = Assert.Single(
            result.Lines.Select(PlainText),
            line => line.Contains("complete", StringComparison.Ordinal));

        Assert.Equal("Reasoning complete - Ctrl+2 for details", summary);
        Assert.All(summary, character => Assert.InRange(character, '\0', '\u007F'));
    }

    [Fact]
    public void Build_ThoughtInspectorKeepsCompletedThoughtExpanded()
    {
        TimelineLayoutResult result = TerminalTimelineLayout.Build(
            [Entry("done", ChatEntryKind.Thought, "Reasoning", "Compared all records")],
            width: 80,
            TimelineGlyphSet.Unicode,
            collapseCompletedThoughts: false);

        Assert.Contains(result.Lines, line => PlainText(line) == "Compared all records");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Build_NarrowWidthsNeverProduceOverwideLinesOrThrow(int width)
    {
        TimelineLayoutResult result = TerminalTimelineLayout.Build(
            [Entry("a", ChatEntryKind.Agent, "Agent", "longcontent")],
            width,
            TimelineGlyphSet.Ascii,
            collapseCompletedThoughts: true);

        Assert.All(result.Lines, line =>
            Assert.True(PlainText(line).Length <= Math.Max(1, width)));
    }

    [Fact]
    public void Build_TracksRowRangesWithoutCountingSeparators()
    {
        TimelineLayoutResult result = TerminalTimelineLayout.Build(
            [
                Entry("first", ChatEntryKind.Agent, "Agent", "One"),
                Entry("second", ChatEntryKind.Agent, "Agent", "Two")
            ],
            width: 40,
            TimelineGlyphSet.Unicode,
            collapseCompletedThoughts: true);

        Assert.Equal(new TimelineRowRange(0, 2), result.EntryRows["first"]);
        Assert.Equal(new TimelineRowRange(3, 2), result.EntryRows["second"]);
        Assert.Equal(5, result.Lines.Count);
        Assert.Equal(string.Empty, PlainText(result.Lines[2]));
    }

    [Fact]
    public void Build_TruncatesHeaderWithoutSplittingSurrogatePairs()
    {
        const int width = 5;
        TimelineLayoutResult result = TerminalTimelineLayout.Build(
            [Entry("emoji", ChatEntryKind.Agent, "🙂Alpha", string.Empty)],
            width,
            TimelineGlyphSet.Ascii,
            collapseCompletedThoughts: true);

        string header = PlainText(result.Lines[0]);

        Assert.Equal("*  🙂", header);
        Assert.True(IsWellFormedUtf16(header));
        Assert.True(header.GetColumns() <= width);
    }

    [Fact]
    public void Build_WrapsBodyWithoutSplittingSurrogatePairs()
    {
        const int width = 2;
        TimelineLayoutResult result = TerminalTimelineLayout.Build(
            [Entry("emoji", ChatEntryKind.Agent, "Agent", "🙂🙂")],
            width,
            TimelineGlyphSet.Ascii,
            collapseCompletedThoughts: true);

        Assert.Equal(["* ", "🙂", "🙂"], result.Lines.Select(PlainText));
        Assert.All(result.Lines, line =>
        {
            Assert.True(IsWellFormedUtf16(PlainText(line)));
            Assert.True(PlainText(line).GetColumns() <= width);
        });
    }

    [Fact]
    public void Build_WrapsCjkAtTerminalCellWidth()
    {
        const int width = 4;
        TimelineLayoutResult result = TerminalTimelineLayout.Build(
            [Entry("cjk", ChatEntryKind.Agent, "A", "界界界")],
            width,
            TimelineGlyphSet.Ascii,
            collapseCompletedThoughts: true);

        Assert.Equal(["*  A", "界界", "界"], result.Lines.Select(PlainText));
        Assert.All(result.Lines, line => Assert.True(PlainText(line).GetColumns() <= width));
    }

    [Fact]
    public void Build_WrapsEmojiWithoutSplittingCombiningGraphemes()
    {
        const int width = 4;
        TimelineLayoutResult result = TerminalTimelineLayout.Build(
            [Entry("graphemes", ChatEntryKind.Agent, "A", "e\u0301e\u0301🙂🙂")],
            width,
            TimelineGlyphSet.Ascii,
            collapseCompletedThoughts: true);

        Assert.Equal(["*  A", "e\u0301e\u0301🙂", "🙂"], result.Lines.Select(PlainText));
        Assert.All(result.Lines, line =>
        {
            Assert.True(IsWellFormedUtf16(PlainText(line)));
            Assert.True(PlainText(line).GetColumns() <= width);
        });
    }

    [Fact]
    public void Build_ReplacesSingleGraphemeThatCannotFitOneTerminalCell()
    {
        const int width = 1;
        TimelineLayoutResult result = TerminalTimelineLayout.Build(
            [Entry("wide", ChatEntryKind.Agent, string.Empty, "界")],
            width,
            TimelineGlyphSet.Ascii,
            collapseCompletedThoughts: true);

        Assert.Equal(["*", "?"], result.Lines.Select(PlainText));
        Assert.All(result.Lines, line => Assert.True(PlainText(line).GetColumns() <= width));
    }

    [Fact]
    public void Build_PreservesLiteralUnicodeEscapeAsText()
    {
        TimelineLayoutResult result = TerminalTimelineLayout.Build(
            [Entry("escape", ChatEntryKind.Agent, "Agent", "prefix \\u1234 suffix")],
            width: 40,
            TimelineGlyphSet.Ascii,
            collapseCompletedThoughts: true);

        Assert.Equal(["*  Agent", "prefix \\u1234 suffix"], result.Lines.Select(PlainText));
    }

    [Fact]
    public void Build_ExpandsTabsBeforeMeasuringBodyWidth()
    {
        TimelineLayoutResult result = TerminalTimelineLayout.Build(
            [Entry("tab", ChatEntryKind.Agent, "Agent", "a\tb")],
            width: 4,
            TimelineGlyphSet.Ascii,
            collapseCompletedThoughts: true);

        Assert.DoesNotContain('\t', PlainText(result.Lines[1]));
        Assert.All(result.Lines, line => Assert.True(DisplayWidth(PlainText(line)) <= 4));
    }

    private static ChatEntry Entry(
        string key,
        ChatEntryKind kind,
        string author,
        string text,
        bool isTransient = false)
    {
        return new ChatEntry(key, kind, author, text, isTransient, [], [], key);
    }

    private static string PlainText(TimelineLine line)
    {
        return string.Concat(line.Spans.Select(span => span.Text));
    }

    private static bool IsWellFormedUtf16(string value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (char.IsHighSurrogate(current))
            {
                if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                {
                    return false;
                }

                index++;
                continue;
            }

            if (char.IsLowSurrogate(current))
            {
                return false;
            }
        }

        return true;
    }

    private static int DisplayWidth(string value)
    {
        const int tabStop = 8;
        int width = 0;

        foreach (char character in value)
        {
            if (character == '\t')
            {
                width += tabStop - (width % tabStop);
                continue;
            }

            width++;
        }

        return width;
    }
}
