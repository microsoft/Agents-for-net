#nullable enable

using System.Collections.Generic;
using System.Globalization;
using System.Text;

internal enum TimelineRole
{
    Normal,
    Accent,
    Success,
    Muted,
    Warning
}

internal sealed record TimelineSpan(string Text, TimelineRole Role);

internal sealed record TimelineLine(string EntryKey, IReadOnlyList<TimelineSpan> Spans);

internal sealed record TimelineLayoutResult(
    IReadOnlyList<TimelineLine> Lines,
    IReadOnlyDictionary<string, TimelineRowRange> EntryRows);

internal readonly record struct TimelineRowRange(int Start, int Count);

internal sealed record TimelineGlyphSet(
    string User,
    string Agent,
    string Status,
    string Thought,
    string Event,
    string Attachment,
    string Diagnostic)
{
    internal static TimelineGlyphSet Unicode { get; } =
        new(">", "●", "○", "◆", "↗", "▣", "!");

    internal static TimelineGlyphSet Ascii { get; } =
        new(">", "*", "o", "~", "^", "+", "!");

    internal static TimelineGlyphSet ForEncoding(Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        return encoding.CodePage == Encoding.UTF8.CodePage ? Unicode : Ascii;
    }
}

internal static class TerminalTimelineLayout
{
    internal static TimelineLayoutResult Build(
        IReadOnlyList<ChatEntry> entries,
        int width,
        TimelineGlyphSet glyphs,
        bool collapseCompletedThoughts)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(glyphs);

        int contentWidth = Math.Max(1, width);
        List<TimelineLine> lines = new(entries.Count * 3);
        Dictionary<string, TimelineRowRange> entryRows = new(entries.Count, StringComparer.Ordinal);

        for (int index = 0; index < entries.Count; index++)
        {
            ChatEntry entry = entries[index];
            int start = lines.Count;

            (string headerGlyph, TimelineRole headerRole) = GetHeader(entry.Kind, glyphs);
            string headerText = BuildHeaderText(headerGlyph, SanitizeSingleLine(entry.Author), contentWidth);
            lines.Add(new TimelineLine(entry.Key, [new TimelineSpan(headerText, headerRole)]));

            string bodyText = GetBodyText(entry, collapseCompletedThoughts);
            TimelineRole bodyRole = GetBodyRole(entry.Kind);
            foreach (string line in Wrap(bodyText, contentWidth))
            {
                lines.Add(new TimelineLine(entry.Key, [new TimelineSpan(line, bodyRole)]));
            }

            int count = lines.Count - start;
            if (index < entries.Count - 1)
            {
                lines.Add(new TimelineLine(entry.Key, []));
            }

            entryRows.Add(entry.Key, new TimelineRowRange(start, count));
        }

        return new TimelineLayoutResult(lines, entryRows);
    }

    private static (string Glyph, TimelineRole Role) GetHeader(
        ChatEntryKind kind,
        TimelineGlyphSet glyphs) => kind switch
    {
        ChatEntryKind.User => (glyphs.User, TimelineRole.Accent),
        ChatEntryKind.Agent => (glyphs.Agent, TimelineRole.Success),
        ChatEntryKind.Status => (glyphs.Status, TimelineRole.Muted),
        ChatEntryKind.Thought => (glyphs.Thought, TimelineRole.Accent),
        ChatEntryKind.Event => (glyphs.Event, TimelineRole.Muted),
        ChatEntryKind.Attachment => (glyphs.Attachment, TimelineRole.Accent),
        ChatEntryKind.Diagnostic => (glyphs.Diagnostic, TimelineRole.Warning),
        _ => (glyphs.Event, TimelineRole.Normal)
    };

    private static TimelineRole GetBodyRole(ChatEntryKind kind) => kind switch
    {
        ChatEntryKind.User => TimelineRole.Normal,
        ChatEntryKind.Agent => TimelineRole.Normal,
        ChatEntryKind.Status => TimelineRole.Muted,
        ChatEntryKind.Thought => TimelineRole.Muted,
        ChatEntryKind.Event => TimelineRole.Muted,
        ChatEntryKind.Attachment => TimelineRole.Accent,
        ChatEntryKind.Diagnostic => TimelineRole.Warning,
        _ => TimelineRole.Normal
    };

    private static string BuildHeaderText(string glyph, string author, int width)
    {
        string text = string.IsNullOrEmpty(author)
            ? glyph
            : string.Concat(glyph, "  ", author);

        return text.Length <= width ? text : text[..width];
    }

    private static string GetBodyText(ChatEntry entry, bool collapseCompletedThoughts)
    {
        if (entry.Kind == ChatEntryKind.Thought && collapseCompletedThoughts && !entry.IsTransient)
        {
            return string.Concat(SanitizeSingleLine(entry.Author), " complete · Ctrl+2 for details");
        }

        return Sanitize(entry.Text);
    }

    private static IReadOnlyList<string> Wrap(string value, int width)
    {
        string[] paragraphs = Sanitize(value).Split('\n');
        List<string> lines = new();

        foreach (string paragraph in paragraphs)
        {
            WrapParagraph(paragraph, width, lines);
        }

        if (lines.Count == 0)
        {
            lines.Add(string.Empty);
        }

        return lines;
    }

    private static void WrapParagraph(string paragraph, int width, List<string> lines)
    {
        if (paragraph.Length == 0)
        {
            lines.Add(string.Empty);
            return;
        }

        string? current = null;
        int index = 0;
        while (index < paragraph.Length)
        {
            while (index < paragraph.Length && paragraph[index] == ' ')
            {
                index++;
            }

            if (index >= paragraph.Length)
            {
                break;
            }

            int start = index;
            while (index < paragraph.Length && paragraph[index] != ' ')
            {
                index++;
            }

            string token = paragraph[start..index];
            if (IsControlEscapeToken(token))
            {
                if (current is not null)
                {
                    lines.Add(current);
                    current = null;
                }

                lines.Add(token);
                continue;
            }

            if (token.Length > width)
            {
                if (current is not null)
                {
                    lines.Add(current);
                    current = null;
                }

                for (int offset = 0; offset < token.Length; offset += width)
                {
                    lines.Add(token.Substring(offset, Math.Min(width, token.Length - offset)));
                }

                continue;
            }

            if (current is null)
            {
                current = token;
                continue;
            }

            if (current.Length + 1 + token.Length <= width)
            {
                current = string.Concat(current, " ", token);
            }
            else
            {
                lines.Add(current);
                current = token;
            }
        }

        if (current is not null)
        {
            lines.Add(current);
        }
    }

    private static bool IsControlEscapeToken(string token)
    {
        return token.Length == 6
            && token[0] == '\\'
            && token[1] == 'u'
            && IsHexDigit(token[2])
            && IsHexDigit(token[3])
            && IsHexDigit(token[4])
            && IsHexDigit(token[5]);
    }

    private static bool IsHexDigit(char value)
    {
        return (value >= '0' && value <= '9')
            || (value >= 'A' && value <= 'F')
            || (value >= 'a' && value <= 'f');
    }

    private static string Sanitize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        string normalized = value.Replace("\r\n", "\n").Replace('\r', '\n');
        StringBuilder builder = new(normalized.Length);

        foreach (char character in normalized)
        {
            if (character == '\n' || character == '\t')
            {
                builder.Append(character);
                continue;
            }

            if (char.IsControl(character))
            {
                builder.Append(' ');
                builder.Append("\\u");
                builder.Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
                builder.Append(' ');
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static string SanitizeSingleLine(string value)
    {
        return Sanitize(value).Replace('\n', ' ').Trim();
    }
}
