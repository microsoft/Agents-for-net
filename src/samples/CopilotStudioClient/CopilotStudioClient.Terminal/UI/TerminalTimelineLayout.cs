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
    private const int TabStop = 8;
    private static readonly TimelineLine[] NoLines = [];

    private enum RenderTokenKind
    {
        Text,
        ControlEscape,
        Tab
    }

    private readonly record struct RenderToken(RenderTokenKind Kind, string Text, bool LeadingSpace);

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
            string headerText = BuildHeaderText(headerGlyph, entry.Author, contentWidth);
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

        return new TimelineLayoutResult(lines.Count == 0 ? NoLines : lines.ToArray(), entryRows);
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
        string normalizedAuthor = NormalizeInlineText(author);
        string text = string.IsNullOrEmpty(normalizedAuthor)
            ? glyph
            : string.Concat(glyph, "  ", normalizedAuthor);

        return TruncateTextElements(text, width);
    }

    private static string GetBodyText(ChatEntry entry, bool collapseCompletedThoughts)
    {
        if (entry.Kind == ChatEntryKind.Thought && collapseCompletedThoughts && !entry.IsTransient)
        {
            return string.Concat(NormalizeInlineText(entry.Author), " complete · Ctrl+2 for details");
        }

        return entry.Text;
    }

    private static IReadOnlyList<string> Wrap(string value, int width)
    {
        ArgumentNullException.ThrowIfNull(value);

        int contentWidth = Math.Max(1, width);
        string normalized = value.Replace("\r\n", "\n").Replace('\r', '\n');
        List<string> lines = new();

        foreach (string paragraph in normalized.Split('\n'))
        {
            WrapParagraph(TokenizeParagraph(paragraph), contentWidth, lines);
        }

        if (lines.Count == 0)
        {
            lines.Add(string.Empty);
        }

        return lines;
    }

    private static void WrapParagraph(IReadOnlyList<RenderToken> tokens, int width, List<string> lines)
    {
        if (tokens.Count == 0)
        {
            lines.Add(string.Empty);
            return;
        }

        StringBuilder current = new();
        int currentWidth = 0;

        void EmitCurrentLine()
        {
            lines.Add(current.ToString());
            current.Clear();
            currentWidth = 0;
        }

        void EmitTextPieces(string text)
        {
            foreach (string piece in SliceTextElements(text, width))
            {
                lines.Add(piece);
            }
        }

        void EmitTab()
        {
            int tabSpaces = TabStop - (currentWidth % TabStop);
            if (currentWidth > 0 && currentWidth + tabSpaces > width)
            {
                EmitCurrentLine();
                tabSpaces = TabStop;
            }

            while (tabSpaces > 0)
            {
                if (currentWidth == width)
                {
                    EmitCurrentLine();
                }

                int available = width - currentWidth;
                if (available == 0)
                {
                    EmitCurrentLine();
                    continue;
                }

                int toWrite = Math.Min(tabSpaces, available);
                current.Append(' ', toWrite);
                currentWidth += toWrite;
                tabSpaces -= toWrite;

                if (currentWidth == width && tabSpaces > 0)
                {
                    EmitCurrentLine();
                }
            }
        }

        void AppendTextToken(RenderToken token, bool treatAsControlEscape)
        {
            int tokenWidth = MeasureTextElements(token.Text);

            if (treatAsControlEscape)
            {
                if (currentWidth > 0)
                {
                    EmitCurrentLine();
                }

                EmitTextPieces(token.Text);
                return;
            }

            if (token.LeadingSpace && currentWidth > 0)
            {
                if (currentWidth + 1 + tokenWidth > width)
                {
                    EmitCurrentLine();
                }
                else
                {
                    current.Append(' ');
                    currentWidth++;
                }
            }

            if (tokenWidth > width)
            {
                if (currentWidth > 0)
                {
                    EmitCurrentLine();
                }

                EmitTextPieces(token.Text);
                return;
            }

            if (currentWidth + tokenWidth > width)
            {
                EmitCurrentLine();
            }

            current.Append(token.Text);
            currentWidth += tokenWidth;
        }

        foreach (RenderToken token in tokens)
        {
            switch (token.Kind)
            {
                case RenderTokenKind.Tab:
                    EmitTab();
                    break;
                case RenderTokenKind.ControlEscape:
                    AppendTextToken(token, treatAsControlEscape: true);
                    break;
                case RenderTokenKind.Text:
                    AppendTextToken(token, treatAsControlEscape: false);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported token kind {token.Kind}.");
            }
        }

        if (currentWidth > 0 || current.Length > 0)
        {
            EmitCurrentLine();
        }
    }

    private static IReadOnlyList<RenderToken> TokenizeParagraph(string paragraph)
    {
        List<RenderToken> tokens = new();
        StringBuilder current = new();
        bool leadingSpace = false;

        void FlushCurrentWord()
        {
            if (current.Length == 0)
            {
                return;
            }

            tokens.Add(new RenderToken(RenderTokenKind.Text, current.ToString(), leadingSpace));
            current.Clear();
            leadingSpace = false;
        }

        foreach (string textElement in EnumerateTextElements(paragraph))
        {
            if (textElement == " ")
            {
                if (current.Length > 0)
                {
                    FlushCurrentWord();
                }

                leadingSpace = true;
                continue;
            }

            if (textElement == "\t")
            {
                FlushCurrentWord();
                tokens.Add(new RenderToken(RenderTokenKind.Tab, string.Empty, false));
                leadingSpace = false;
                continue;
            }

            if (textElement.Length == 1 && char.IsControl(textElement[0]))
            {
                FlushCurrentWord();
                tokens.Add(new RenderToken(RenderTokenKind.ControlEscape, EscapeControl(textElement[0]), leadingSpace));
                leadingSpace = false;
                continue;
            }

            current.Append(textElement);
        }

        FlushCurrentWord();
        return tokens;
    }

    private static string NormalizeInlineText(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        string normalized = value.Replace("\r\n", "\n").Replace('\r', '\n');
        StringBuilder builder = new(normalized.Length);
        int column = 0;

        foreach (string textElement in EnumerateTextElements(normalized))
        {
            if (textElement == "\n")
            {
                builder.Append(' ');
                column++;
                continue;
            }

            if (textElement == "\t")
            {
                int spaces = TabStop - (column % TabStop);
                builder.Append(' ', spaces);
                column += spaces;
                continue;
            }

            if (textElement.Length == 1 && char.IsControl(textElement[0]))
            {
                builder.Append(' ');
                builder.Append(EscapeControl(textElement[0]));
                builder.Append(' ');
                column += 8;
                continue;
            }

            builder.Append(textElement);
            column++;
        }

        return builder.ToString().Trim();
    }

    private static string TruncateTextElements(string value, int width)
    {
        int remaining = Math.Max(1, width);
        StringBuilder builder = new();

        foreach (string textElement in EnumerateTextElements(value))
        {
            if (remaining == 0)
            {
                break;
            }

            builder.Append(textElement);
            remaining--;
        }

        return builder.ToString();
    }

    private static int MeasureTextElements(string value)
    {
        int count = 0;
        foreach (string _ in EnumerateTextElements(value))
        {
            count++;
        }

        return count;
    }

    private static IEnumerable<string> SliceTextElements(string value, int width)
    {
        int remaining = Math.Max(1, width);
        StringBuilder builder = new();

        foreach (string textElement in EnumerateTextElements(value))
        {
            builder.Append(textElement);
            remaining--;
            if (remaining == 0)
            {
                yield return builder.ToString();
                builder.Clear();
                remaining = Math.Max(1, width);
            }
        }

        if (builder.Length > 0)
        {
            yield return builder.ToString();
        }
    }

    private static IEnumerable<string> EnumerateTextElements(string value)
    {
        TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(value);
        while (enumerator.MoveNext())
        {
            yield return (string)enumerator.Current!;
        }
    }

    private static string EscapeControl(char character)
    {
        return string.Concat("\\u", ((int)character).ToString("X4", CultureInfo.InvariantCulture));
    }
}
