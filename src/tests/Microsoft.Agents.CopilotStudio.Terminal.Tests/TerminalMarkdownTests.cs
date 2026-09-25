#nullable enable

using System.Collections.Generic;
using System.Linq;

public sealed class TerminalMarkdownTests
{
    [Fact]
    public void Parse_EmitsHeadingAndInlineStylesWithoutDelimiters()
    {
        IReadOnlyList<TimelineBlock> blocks = TerminalMarkdown.Parse(
            "# Result\nThis is **bold**, *italic*, and `code`.",
            TimelineRole.Agent);

        Assert.Equal(TimelineBlockKind.Heading1, blocks[0].Kind);
        Assert.Equal("Result", Assert.Single(blocks[0].Spans).Text);
        Assert.Equal(TimelineTextStyle.Bold, blocks[0].Spans[0].Style);
        Assert.Contains(blocks[1].Spans, span =>
            span.Text == "bold" && span.Style.HasFlag(TimelineTextStyle.Bold));
        Assert.Contains(blocks[1].Spans, span =>
            span.Text == "italic" && span.Style.HasFlag(TimelineTextStyle.Italic));
        Assert.Contains(blocks[1].Spans, span =>
            span.Text == "code" && span.Role == TimelineRole.Code);
        Assert.DoesNotContain("**", PlainText(blocks));
    }

    [Fact]
    public void Parse_EmitsListMetadataAndSafeLinkTarget()
    {
        IReadOnlyList<TimelineBlock> blocks = TerminalMarkdown.Parse(
            "- first\n2. [Docs](https://example.com/docs)",
            TimelineRole.Agent);

        Assert.Equal(TimelineBlockKind.UnorderedListItem, blocks[0].Kind);
        Assert.Equal(TimelineBlockKind.OrderedListItem, blocks[1].Kind);
        Assert.Equal(2, blocks[1].Ordinal);
        TimelineSpan link = Assert.Single(blocks[1].Spans, span => span.LinkTarget is not null);
        Assert.Equal("Docs", link.Text);
        Assert.Equal("https://example.com/docs", link.LinkTarget!.AbsoluteUri);
        Assert.Equal(TimelineRole.Link, link.Role);
        Assert.True(link.Style.HasFlag(TimelineTextStyle.Underline));
    }

    [Theory]
    [InlineData("**unclosed")]
    [InlineData("[unsafe](file:///C:/secret.txt)")]
    [InlineData("<b>not interpreted</b>")]
    public void Parse_MalformedOrUnsupportedInputFallsBackToSanitizedPlainText(string input)
    {
        IReadOnlyList<TimelineBlock> blocks = TerminalMarkdown.Parse(input, TimelineRole.Agent);

        Assert.Equal(input, PlainText(blocks));
        Assert.All(blocks.SelectMany(block => block.Spans), span => Assert.Null(span.LinkTarget));
    }

    [Fact]
    public void Parse_EscapesControlsBeforeReturningSpans()
    {
        IReadOnlyList<TimelineBlock> blocks =
            TerminalMarkdown.Parse("alpha\u001Bbeta", TimelineRole.Agent);

        Assert.Equal("alpha\\u001Bbeta", PlainText(blocks));
    }

    [Theory]
    [InlineData("file_name_with_underscores")]
    [InlineData(@"C:\repo_name\file_name.txt")]
    [InlineData("https://example.com/path_with_underscores")]
    public void Parse_DoesNotTreatIntrawordUnderscoresAsEmphasis(string input)
    {
        IReadOnlyList<TimelineBlock> blocks = TerminalMarkdown.Parse(input, TimelineRole.Agent);

        Assert.Equal(input, PlainText(blocks));
        Assert.DoesNotContain(
            blocks.SelectMany(block => block.Spans),
            span => span.Style.HasFlag(TimelineTextStyle.Italic));
    }

    [Fact]
    public void Parse_EmitsSingleUnderscoreEmphasisAtWordBoundaries()
    {
        IReadOnlyList<TimelineBlock> blocks =
            TerminalMarkdown.Parse("This is _italic_.", TimelineRole.Agent);

        Assert.Equal("This is italic.", PlainText(blocks));
        TimelineSpan italic = Assert.Single(
            blocks.SelectMany(block => block.Spans),
            span => span.Text == "italic");
        Assert.True(italic.Style.HasFlag(TimelineTextStyle.Italic));
    }

    private static string PlainText(IReadOnlyList<TimelineBlock> blocks)
    {
        return string.Concat(blocks.SelectMany(block => block.Spans).Select(span => span.Text));
    }
}
