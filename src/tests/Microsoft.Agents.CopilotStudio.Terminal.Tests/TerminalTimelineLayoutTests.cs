#nullable enable

using System;
using System.Collections.Generic;
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
            Entry("t", ChatEntryKind.Thought, "Reasoning", "Checking", isTransient: true),
            Entry("e", ChatEntryKind.Event, "Event", "Tool finished"),
            Entry("w", ChatEntryKind.Diagnostic, "System", "Retrying", severity: DiagnosticSeverity.Warning),
            Entry("x", ChatEntryKind.Diagnostic, "System", "Localized fatal condition", severity: DiagnosticSeverity.Error)
        ];

        TimelineLayoutResult result = TerminalTimelineLayout.Build(
            entries,
            width: 40,
            TimelineGlyphSet.Unicode,
            collapseCompletedThoughts: true);

        Assert.Equal(
            [
                ">  You", "Question", "",
                "●  Agent", "Answer", "",
                "○  Status", "Working", "",
                "◆  Reasoning", "Checking", "",
                "↗  Event", "Tool finished", "",
                "!  System", "Retrying", "",
                "!  System", "Localized fatal condition"
            ],
            result.Lines.Select(PlainText));
        Assert.Equal(TimelineRole.User, result.Lines[0].Spans[0].Role);
        Assert.Equal(TimelineRole.User, result.Lines[1].Spans[0].Role);
        Assert.Equal(TimelineRole.Agent, result.Lines[3].Spans[0].Role);
        Assert.Equal(TimelineRole.Agent, result.Lines[4].Spans[0].Role);
        Assert.Equal(TimelineRole.Muted, result.Lines[6].Spans[0].Role);
        Assert.Equal(TimelineRole.Muted, result.Lines[7].Spans[0].Role);
        Assert.Equal(TimelineRole.Thought, result.Lines[9].Spans[0].Role);
        Assert.Equal(TimelineRole.Thought, result.Lines[10].Spans[0].Role);
        Assert.Equal(TimelineRole.Muted, result.Lines[12].Spans[0].Role);
        Assert.Equal(TimelineRole.Muted, result.Lines[13].Spans[0].Role);
        Assert.Equal(TimelineRole.Warning, result.Lines[15].Spans[0].Role);
        Assert.Equal(TimelineRole.Warning, result.Lines[16].Spans[0].Role);
        Assert.Equal(TimelineRole.Error, result.Lines[18].Spans[0].Role);
        Assert.Equal(TimelineRole.Error, result.Lines[19].Spans[0].Role);
    }

    [Fact]
    public void Build_StylesMarkdownBodyButNeverParsesAuthorHeader()
    {
        TimelineLayoutResult result = TerminalTimelineLayout.Build(
            [Entry("a", ChatEntryKind.Agent, "**Agent**", "**Answer** and `code`")],
            width: 40,
            TimelineGlyphSet.Unicode,
            collapseCompletedThoughts: true);

        Assert.Equal("●  **Agent**", PlainText(result.Lines[0]));
        Assert.Equal("Answer and code", PlainText(result.Lines[1]));
        Assert.Contains(result.Lines[1].Spans, span =>
            span.Text == "Answer" && span.Style == TimelineTextStyle.Bold);
        Assert.Contains(result.Lines[1].Spans, span =>
            span.Text == "code" && span.Role == TimelineRole.Code);
    }

    [Fact]
    public void Build_ReplacedStreamingMarkdownKeepsOneRangeAndStyledText()
    {
        TimelineLayoutResult result = TerminalTimelineLayout.Build(
            [Entry("stream", ChatEntryKind.Agent, "Agent", "**Hello**", isTransient: true)],
            width: 40,
            TimelineGlyphSet.Unicode,
            collapseCompletedThoughts: true);

        KeyValuePair<string, TimelineRowRange> range = Assert.Single(result.EntryRows);
        Assert.Equal("stream", range.Key);
        Assert.Equal(new TimelineRowRange(0, 2), range.Value);
        Assert.Equal(["●  Agent", "Hello"], result.Lines.Select(PlainText));
        TimelineSpan body = Assert.Single(result.Lines[1].Spans);
        Assert.Equal("Hello", body.Text);
        Assert.True(body.Style.HasFlag(TimelineTextStyle.Bold));
    }

    [Fact]
    public void Build_WrapsStyledCjkAndEmojiAtCellWidthWithoutLosingMetadata()
    {
        TimelineLayoutResult result = TerminalTimelineLayout.Build(
            [Entry("a", ChatEntryKind.Agent, "A", "**界界**🙂🙂")],
            width: 4,
            TimelineGlyphSet.Ascii,
            collapseCompletedThoughts: true);

        Assert.All(result.Lines, line => Assert.True(PlainText(line).GetColumns() <= 4));
        Assert.All(
            result.Lines.SelectMany(line => line.Spans).Where(span => span.Text.Contains('界')),
            span => Assert.True(span.Style.HasFlag(TimelineTextStyle.Bold)));
        Assert.All(result.Lines, line => Assert.True(IsWellFormedUtf16(PlainText(line))));
    }

    [Fact]
    public void Build_CollapsedThoughtUsesPortableF2Hint()
    {
        TimelineLayoutResult result = TerminalTimelineLayout.Build(
            [Entry("done", ChatEntryKind.Thought, "Reasoning", "Complete")],
            width: 80,
            TimelineGlyphSet.Unicode,
            collapseCompletedThoughts: true);

        Assert.Contains(result.Lines, line => PlainText(line) == "Reasoning complete · F2 for details");
    }

    [Fact]
    public void Build_DiagnosticRoleUsesSeverityInsteadOfText()
    {
        ChatEntry[] entries =
        [
            Entry(
                "warning",
                ChatEntryKind.Diagnostic,
                "System",
                "Warning message mentions failed retries.",
                severity: DiagnosticSeverity.Warning),
            Entry(
                "error",
                ChatEntryKind.Diagnostic,
                "System",
                "Localized fatal condition.",
                severity: DiagnosticSeverity.Error)
        ];

        TimelineLayoutResult result = TerminalTimelineLayout.Build(
            entries,
            width: 80,
            TimelineGlyphSet.Unicode,
            collapseCompletedThoughts: true);

        TimelineLine warningBody = Assert.Single(
            result.Lines,
            line => line.EntryKey == "warning" && PlainText(line).Contains("failed", StringComparison.Ordinal));
        TimelineLine errorBody = Assert.Single(
            result.Lines,
            line => line.EntryKey == "error" && PlainText(line).Contains("Localized", StringComparison.Ordinal));
        Assert.Equal(TimelineRole.Warning, Assert.Single(warningBody.Spans).Role);
        Assert.Equal(TimelineRole.Error, Assert.Single(errorBody.Spans).Role);
    }

    [Fact]
    public void Build_EscapesRealControlsWithoutDecodingLiteralPrivateUseSentinelText()
    {
        const string literalSentinelText = "\uE000001B\uE001";
        TimelineLayoutResult result = TerminalTimelineLayout.Build(
            [Entry("a", ChatEntryKind.Agent, "Agent", $"literal {literalSentinelText} real \u001B")],
            width: 80,
            TimelineGlyphSet.Ascii,
            collapseCompletedThoughts: true);

        Assert.Contains(result.Lines, line => PlainText(line) == $"literal {literalSentinelText} real");
        Assert.Contains(result.Lines, line => PlainText(line) == "\\u001B");
    }

    [Fact]
    public void Build_DoesNotParseMarkdownInNonConversationalBodies()
    {
        ChatEntry[] entries =
        [
            Entry("status", ChatEntryKind.Status, "Status", "**not bold** file_name_*", isTransient: true),
            Entry("event", ChatEntryKind.Event, "Event", "[literal](https://example.com) *event*"),
            Entry("attachment", ChatEntryKind.Attachment, "Agent", "report_1_*final*.md"),
            Entry(
                "diagnostic",
                ChatEntryKind.Diagnostic,
                "System",
                "**warning** for path_*",
                severity: DiagnosticSeverity.Warning)
        ];

        TimelineLayoutResult result = TerminalTimelineLayout.Build(
            entries,
            width: 80,
            TimelineGlyphSet.Unicode,
            collapseCompletedThoughts: true);

        Assert.Contains(result.Lines, line => PlainText(line) == "**not bold** file_name_*");
        Assert.Contains(result.Lines, line => PlainText(line) == "[literal](https://example.com) *event*");
        Assert.Contains(result.Lines, line => PlainText(line) == "report_1_*final*.md");
        Assert.Contains(result.Lines, line => PlainText(line) == "**warning** for path_*");
        Assert.DoesNotContain(
            result.Lines.Where(line => line.EntryKey is "status" or "event" or "attachment" or "diagnostic")
                .SelectMany(line => line.Spans),
            span => span.Style != TimelineTextStyle.None || span.LinkTarget is not null || span.Role == TimelineRole.Code);
    }

    [Fact]
    public void WrapBlocks_FormatsBlocksAndPreservesOnlySourceSpanMetadata()
    {
        Uri target = new("https://example.com/docs");
        IReadOnlyList<TimelineLine> lines = TerminalTimelineLayout.WrapBlocks(
            [
                new TimelineBlock(
                    TimelineBlockKind.Heading1,
                    [new TimelineSpan("Title", TimelineRole.Agent)]),
                new TimelineBlock(
                    TimelineBlockKind.UnorderedListItem,
                    [new TimelineSpan("Docs", TimelineRole.Link, TimelineTextStyle.Underline, target)]),
                new TimelineBlock(
                    TimelineBlockKind.OrderedListItem,
                    [new TimelineSpan("界界", TimelineRole.Agent, TimelineTextStyle.Bold)],
                    7)
            ],
            width: 20);

        Assert.Equal("Title", PlainText(lines[0]));
        Assert.All(lines[0].Spans, span => Assert.True(span.Style.HasFlag(TimelineTextStyle.Bold)));
        Assert.Equal("- Docs", PlainText(lines[1]));
        Assert.Equal("- ", lines[1].Spans[0].Text);
        Assert.Null(lines[1].Spans[0].LinkTarget);
        TimelineSpan link = Assert.Single(lines.SelectMany(line => line.Spans), span => span.LinkTarget is not null);
        Assert.Equal("Docs", link.Text);
        Assert.Equal(target, link.LinkTarget);
        Assert.Contains(lines, line => PlainText(line) == "7. 界界");
        Assert.All(
            lines.SelectMany(line => line.Spans).Where(span => span.Text.Contains('界')),
            span => Assert.True(span.Style.HasFlag(TimelineTextStyle.Bold)));
    }

    [Fact]
    public void Build_ProjectsOnlySafeMarkdownLinksWithEntryIdentity()
    {
        TimelineLayoutResult result = TerminalTimelineLayout.Build(
            [
                Entry(
                    "safe-entry",
                    ChatEntryKind.Agent,
                    "Agent",
                    "[Docs](https://example.com/docs)"),
                Entry(
                    "unsafe-entry",
                    ChatEntryKind.Agent,
                    "Agent",
                    "[Local](file:///C:/secret.txt)")
            ],
            width: 80,
            TimelineGlyphSet.Unicode,
            collapseCompletedThoughts: true);

        TimelineLink link = Assert.Single(result.Links);
        Assert.Equal("safe-entry", link.EntryKey);
        Assert.Equal("Docs", link.Text);
        Assert.Equal("https://example.com/docs", link.Target.AbsoluteUri);
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
            line => PlainText(line) == "Reasoning complete · F2 for details");
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

        Assert.Equal("Reasoning complete - F2 for details", summary);
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
        bool isTransient = false,
        DiagnosticSeverity? severity = null)
    {
        return new ChatEntry(key, kind, author, text, isTransient, [], [], key, severity);
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
