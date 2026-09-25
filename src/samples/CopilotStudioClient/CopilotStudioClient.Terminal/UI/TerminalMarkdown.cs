#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

internal enum TimelineBlockKind
{
    Paragraph,
    Heading1,
    Heading2,
    Heading3,
    UnorderedListItem,
    OrderedListItem
}

internal sealed record TimelineBlock(
    TimelineBlockKind Kind,
    IReadOnlyList<TimelineSpan> Spans,
    int? Ordinal = null,
    int InitialIndent = 0,
    int ContinuationIndent = 0);

internal static class TerminalMarkdown
{
    private static readonly IReadOnlyList<TimelineSpan> NoSpans = Array.AsReadOnly(Array.Empty<TimelineSpan>());

    private readonly record struct InlineParseResult(
        int Consumed,
        IReadOnlyList<TimelineSpan>? Spans = null,
        string? PlainText = null);

    internal static IReadOnlyList<TimelineBlock> Parse(string value, TimelineRole baseRole)
    {
        ArgumentNullException.ThrowIfNull(value);

        string normalized = Sanitize(value).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        List<TimelineBlock> blocks = [];

        foreach (string line in normalized.Split('\n'))
        {
            (TimelineBlockKind kind, string content, int? ordinal, TimelineTextStyle blockStyle) = ParseBlockPrefix(line);
            blocks.Add(new TimelineBlock(kind, ParseInline(content, baseRole, blockStyle), ordinal));
        }

        return Array.AsReadOnly(blocks.ToArray());
    }

    private static (TimelineBlockKind Kind, string Content, int? Ordinal, TimelineTextStyle Style) ParseBlockPrefix(
        string line)
    {
        if (TryParseHeading(line, "###", TimelineBlockKind.Heading3, out string? heading3))
        {
            return (TimelineBlockKind.Heading3, heading3, null, TimelineTextStyle.Bold);
        }

        if (TryParseHeading(line, "##", TimelineBlockKind.Heading2, out string? heading2))
        {
            return (TimelineBlockKind.Heading2, heading2, null, TimelineTextStyle.Bold);
        }

        if (TryParseHeading(line, "#", TimelineBlockKind.Heading1, out string? heading1))
        {
            return (TimelineBlockKind.Heading1, heading1, null, TimelineTextStyle.Bold);
        }

        if (TryParseOrderedList(line, out string? orderedContent, out int ordinal))
        {
            return (TimelineBlockKind.OrderedListItem, orderedContent, ordinal, TimelineTextStyle.None);
        }

        if (TryParseUnorderedList(line, out string? unorderedContent))
        {
            return (TimelineBlockKind.UnorderedListItem, unorderedContent, null, TimelineTextStyle.None);
        }

        return (TimelineBlockKind.Paragraph, line, null, TimelineTextStyle.None);
    }

    private static IReadOnlyList<TimelineSpan> ParseInline(
        string content,
        TimelineRole baseRole,
        TimelineTextStyle inheritedStyle)
    {
        if (content.Length == 0)
        {
            return NoSpans;
        }

        List<TimelineSpan> spans = [];
        StringBuilder plainText = new();
        int index = 0;

        void FlushPlainText()
        {
            if (plainText.Length == 0)
            {
                return;
            }

            AddSpan(spans, new TimelineSpan(plainText.ToString(), baseRole, inheritedStyle));
            plainText.Clear();
        }

        while (index < content.Length)
        {
            if (TryParseInlineConstruct(content, index, baseRole, inheritedStyle, out InlineParseResult result))
            {
                if (result.Spans is not null)
                {
                    FlushPlainText();
                    foreach (TimelineSpan span in result.Spans)
                    {
                        AddSpan(spans, span);
                    }
                }
                else if (!string.IsNullOrEmpty(result.PlainText))
                {
                    plainText.Append(result.PlainText);
                }

                index += result.Consumed;
                continue;
            }

            plainText.Append(content[index]);
            index++;
        }

        FlushPlainText();
        return spans.Count == 0 ? NoSpans : Array.AsReadOnly(spans.ToArray());
    }

    private static bool TryParseInlineConstruct(
        string content,
        int index,
        TimelineRole baseRole,
        TimelineTextStyle inheritedStyle,
        out InlineParseResult result)
    {
        if (TryParseLink(content, index, inheritedStyle, out result))
        {
            return true;
        }

        if (TryParseCode(content, index, inheritedStyle, out result))
        {
            return true;
        }

        if (TryParseBold(content, index, baseRole, inheritedStyle, out result))
        {
            return true;
        }

        if (TryParseItalic(content, index, '*', baseRole, inheritedStyle, out result))
        {
            return true;
        }

        if (TryParseItalic(content, index, '_', baseRole, inheritedStyle, out result))
        {
            return true;
        }

        result = default;
        return false;
    }

    private static bool TryParseLink(
        string content,
        int index,
        TimelineTextStyle inheritedStyle,
        out InlineParseResult result)
    {
        if (content[index] != '[')
        {
            result = default;
            return false;
        }

        int labelEnd = content.IndexOf(']', index + 1);
        if (labelEnd < 0)
        {
            result = new InlineParseResult(content.Length - index, PlainText: content[index..]);
            return true;
        }

        if (labelEnd + 1 >= content.Length || content[labelEnd + 1] != '(')
        {
            result = new InlineParseResult(labelEnd - index + 1, PlainText: content[index..(labelEnd + 1)]);
            return true;
        }

        int urlEnd = content.IndexOf(')', labelEnd + 2);
        if (urlEnd < 0)
        {
            result = new InlineParseResult(content.Length - index, PlainText: content[index..]);
            return true;
        }

        string label = content[(index + 1)..labelEnd];
        string targetText = content[(labelEnd + 2)..urlEnd];
        if (string.IsNullOrEmpty(label)
            || !Uri.TryCreate(targetText, UriKind.Absolute, out Uri? target)
            || !IsHttpLink(target))
        {
            result = new InlineParseResult(urlEnd - index + 1, PlainText: content[index..(urlEnd + 1)]);
            return true;
        }

        result = new InlineParseResult(
            urlEnd - index + 1,
            [new TimelineSpan(label, TimelineRole.Link, inheritedStyle | TimelineTextStyle.Underline, target)]);
        return true;
    }

    private static bool TryParseCode(
        string content,
        int index,
        TimelineTextStyle inheritedStyle,
        out InlineParseResult result)
    {
        if (content[index] != '`')
        {
            result = default;
            return false;
        }

        int close = content.IndexOf('`', index + 1);
        if (close < 0)
        {
            result = new InlineParseResult(content.Length - index, PlainText: content[index..]);
            return true;
        }

        string code = content[(index + 1)..close];
        if (code.Length == 0)
        {
            result = new InlineParseResult(close - index + 1, PlainText: content[index..(close + 1)]);
            return true;
        }

        result = new InlineParseResult(
            close - index + 1,
            [new TimelineSpan(code, TimelineRole.Code, inheritedStyle)]);
        return true;
    }

    private static bool TryParseBold(
        string content,
        int index,
        TimelineRole baseRole,
        TimelineTextStyle inheritedStyle,
        out InlineParseResult result)
    {
        if (index + 1 >= content.Length || content[index] != '*' || content[index + 1] != '*')
        {
            result = default;
            return false;
        }

        int close = FindClosingDoubleAsterisk(content, index + 2);
        if (close < 0)
        {
            result = new InlineParseResult(content.Length - index, PlainText: content[index..]);
            return true;
        }

        string inner = content[(index + 2)..close];
        if (inner.Length == 0)
        {
            result = new InlineParseResult(close - index + 2, PlainText: content[index..(close + 2)]);
            return true;
        }

        result = new InlineParseResult(
            close - index + 2,
            ParseInline(inner, baseRole, inheritedStyle | TimelineTextStyle.Bold));
        return true;
    }

    private static bool TryParseItalic(
        string content,
        int index,
        char delimiter,
        TimelineRole baseRole,
        TimelineTextStyle inheritedStyle,
        out InlineParseResult result)
    {
        if (content[index] != delimiter)
        {
            result = default;
            return false;
        }

        if (index + 1 < content.Length && content[index + 1] == delimiter)
        {
            result = default;
            return false;
        }

        if (delimiter == '_' && !CanOpenUnderscore(content, index))
        {
            result = default;
            return false;
        }

        int close = FindClosingSingleDelimiter(content, index + 1, delimiter);
        if (close < 0)
        {
            result = new InlineParseResult(content.Length - index, PlainText: content[index..]);
            return true;
        }

        string inner = content[(index + 1)..close];
        if (inner.Length == 0)
        {
            result = new InlineParseResult(close - index + 1, PlainText: content[index..(close + 1)]);
            return true;
        }

        result = new InlineParseResult(
            close - index + 1,
            ParseInline(inner, baseRole, inheritedStyle | TimelineTextStyle.Italic));
        return true;
    }

    private static bool TryParseHeading(
        string line,
        string marker,
        TimelineBlockKind kind,
        out string content)
    {
        if (line.Equals(marker, StringComparison.Ordinal))
        {
            content = string.Empty;
            return true;
        }

        string prefix = string.Concat(marker, " ");
        if (line.StartsWith(prefix, StringComparison.Ordinal))
        {
            content = line[prefix.Length..];
            return true;
        }

        content = string.Empty;
        return false;
    }

    private static bool TryParseUnorderedList(string line, out string content)
    {
        if (line.Length >= 2 && (line[0] is '-' or '*' or '+') && line[1] == ' ')
        {
            content = line[2..];
            return true;
        }

        content = string.Empty;
        return false;
    }

    private static bool TryParseOrderedList(string line, out string content, out int ordinal)
    {
        int index = 0;
        while (index < line.Length && char.IsDigit(line[index]))
        {
            index++;
        }

        if (index == 0
            || index + 1 >= line.Length
            || line[index] != '.'
            || line[index + 1] != ' '
            || !int.TryParse(line[..index], NumberStyles.None, CultureInfo.InvariantCulture, out ordinal))
        {
            content = string.Empty;
            ordinal = 0;
            return false;
        }

        content = line[(index + 2)..];
        return true;
    }

    private static int FindClosingDoubleAsterisk(string content, int searchStart)
    {
        for (int index = searchStart; index + 1 < content.Length; index++)
        {
            if (content[index] == '*'
                && content[index + 1] == '*'
                && (index == searchStart || content[index - 1] != '*')
                && (index + 2 >= content.Length || content[index + 2] != '*'))
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindClosingSingleDelimiter(string content, int searchStart, char delimiter)
    {
        for (int index = searchStart; index < content.Length; index++)
        {
            if (content[index] == delimiter
                && (index == 0 || content[index - 1] != delimiter)
                && (index + 1 >= content.Length || content[index + 1] != delimiter)
                && (delimiter != '_' || CanCloseUnderscore(content, index)))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool CanOpenUnderscore(string content, int index)
    {
        return index + 1 < content.Length
            && !char.IsWhiteSpace(content[index + 1])
            && (index == 0 || !char.IsLetterOrDigit(content[index - 1]));
    }

    private static bool CanCloseUnderscore(string content, int index)
    {
        return index > 0
            && !char.IsWhiteSpace(content[index - 1])
            && (index + 1 >= content.Length || !char.IsLetterOrDigit(content[index + 1]));
    }

    private static bool IsHttpLink(Uri target)
    {
        return target.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || target.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private static string Sanitize(string value)
    {
        StringBuilder builder = new(value.Length);

        foreach (char character in value)
        {
            if (character is '\r' or '\n')
            {
                builder.Append(character);
                continue;
            }

            if (char.IsControl(character))
            {
                builder.Append("\\u");
                builder.Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static void AddSpan(List<TimelineSpan> spans, TimelineSpan span)
    {
        if (span.Text.Length == 0)
        {
            return;
        }

        if (spans.Count > 0)
        {
            TimelineSpan previous = spans[^1];
            if (previous.Role == span.Role
                && previous.Style == span.Style
                && Equals(previous.LinkTarget, span.LinkTarget))
            {
                spans[^1] = previous with { Text = string.Concat(previous.Text, span.Text) };
                return;
            }
        }

        spans.Add(span);
    }
}
