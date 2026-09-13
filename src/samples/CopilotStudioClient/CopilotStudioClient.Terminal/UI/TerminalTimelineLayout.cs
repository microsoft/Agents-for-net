#nullable enable

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Terminal.Gui.Text;

internal enum TimelineRole
{
    Primary,
    Muted,
    User,
    Agent,
    Thought,
    Link,
    ActiveNavigation,
    Warning,
    Error,
    Code
}

internal sealed record TimelineSpan(
    string Text,
    TimelineRole Role,
    TimelineTextStyle Style = TimelineTextStyle.None,
    Uri? LinkTarget = null);

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
    string Diagnostic,
    string DetailSeparator)
{
    internal static TimelineGlyphSet Unicode { get; } =
        new(">", "●", "○", "◆", "↗", "▣", "!", "·");

    internal static TimelineGlyphSet Ascii { get; } =
        new(">", "*", "o", "~", "^", "+", "!", "-");

    internal static TimelineGlyphSet ForEncoding(Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        return encoding.CodePage == Encoding.UTF8.CodePage ? Unicode : Ascii;
    }
}

internal static class TerminalTimelineLayout
{
    private const int TabStop = 8;
    private const char ControlPlaceholderStart = '\uE000';
    private const char ControlPlaceholderEnd = '\uE001';
    private static readonly ReadOnlyCollection<TimelineLine> NoLines = Array.AsReadOnly(Array.Empty<TimelineLine>());

    private enum RenderTokenKind
    {
        Text,
        ControlEscape,
        Tab
    }

    private readonly record struct RenderToken(
        RenderTokenKind Kind,
        string Text,
        bool LeadingSpace,
        TimelineRole Role,
        TimelineTextStyle Style,
        Uri? LinkTarget,
        TimelineRole LeadingSpaceRole,
        TimelineTextStyle LeadingSpaceStyle,
        Uri? LeadingSpaceLinkTarget);

    private sealed record ControlPlaceholderScope(string Prefix);

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

            TimelineRole entryRole = GetEntryRole(entry);
            (string headerGlyph, TimelineRole headerRole) = GetHeader(entry.Kind, entryRole, glyphs);
            string headerText = BuildHeaderText(headerGlyph, entry.Author, contentWidth);
            lines.Add(new TimelineLine(entry.Key, [new TimelineSpan(headerText, headerRole)]));

            foreach (TimelineLine line in WrapBlocks(GetBodyBlocks(entry, entryRole, glyphs, collapseCompletedThoughts), contentWidth))
            {
                lines.Add(line with { EntryKey = entry.Key });
            }

            int count = lines.Count - start;
            if (index < entries.Count - 1)
            {
                lines.Add(new TimelineLine(entry.Key, []));
            }

            entryRows.Add(entry.Key, new TimelineRowRange(start, count));
        }

        return new TimelineLayoutResult(
            lines.Count == 0 ? NoLines : Array.AsReadOnly(lines.ToArray()),
            entryRows);
    }

    internal static IReadOnlyList<string> WrapText(string value, int width)
    {
        return WrapBlocks(
                [new TimelineBlock(TimelineBlockKind.Paragraph, [new TimelineSpan(value, TimelineRole.Primary)])],
                width)
            .Select(line => string.Concat(line.Spans.Select(span => span.Text)))
            .ToArray();
    }

    internal static IReadOnlyList<TimelineLine> WrapBlocks(
        IReadOnlyList<TimelineBlock> blocks,
        int width)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        int contentWidth = Math.Max(1, width);
        List<TimelineLine> lines = new();

        foreach (TimelineBlock block in blocks)
        {
            WrapParagraph(TokenizeParagraph(GetRenderableSpans(block)), contentWidth, lines);
        }

        if (lines.Count == 0)
        {
            lines.Add(new TimelineLine(string.Empty, []));
        }

        return Array.AsReadOnly(lines.ToArray());
    }

    private static (string Glyph, TimelineRole Role) GetHeader(
        ChatEntryKind kind,
        TimelineRole entryRole,
        TimelineGlyphSet glyphs) => kind switch
    {
        ChatEntryKind.User => (glyphs.User, entryRole),
        ChatEntryKind.Agent => (glyphs.Agent, entryRole),
        ChatEntryKind.Status => (glyphs.Status, entryRole),
        ChatEntryKind.Thought => (glyphs.Thought, entryRole),
        ChatEntryKind.Event => (glyphs.Event, entryRole),
        ChatEntryKind.Attachment => (glyphs.Attachment, entryRole),
        ChatEntryKind.Diagnostic => (glyphs.Diagnostic, entryRole),
        _ => (glyphs.Event, TimelineRole.Primary)
    };

    private static TimelineRole GetEntryRole(ChatEntry entry) => entry.Kind switch
    {
        ChatEntryKind.User => TimelineRole.User,
        ChatEntryKind.Agent => TimelineRole.Agent,
        ChatEntryKind.Status => TimelineRole.Muted,
        ChatEntryKind.Thought => TimelineRole.Thought,
        ChatEntryKind.Event => TimelineRole.Muted,
        ChatEntryKind.Attachment => TimelineRole.Link,
        ChatEntryKind.Diagnostic => entry.Severity == DiagnosticSeverity.Error ? TimelineRole.Error : TimelineRole.Warning,
        _ => TimelineRole.Primary
    };

    private static string BuildHeaderText(string glyph, string author, int width)
    {
        string normalizedAuthor = NormalizeInlineText(author);
        string text = string.IsNullOrEmpty(normalizedAuthor)
            ? glyph
            : string.Concat(glyph, "  ", normalizedAuthor);

        return TruncateToDisplayWidth(text, width);
    }

    private static IReadOnlyList<TimelineBlock> GetBodyBlocks(
        ChatEntry entry,
        TimelineRole role,
        TimelineGlyphSet glyphs,
        bool collapseCompletedThoughts)
    {
        if (entry.Kind == ChatEntryKind.Thought && collapseCompletedThoughts && !entry.IsTransient)
        {
            string summary = string.Concat(
                NormalizeInlineText(entry.Author),
                " complete ",
                glyphs.DetailSeparator,
                " F2 for details");

            return [new TimelineBlock(TimelineBlockKind.Paragraph, [new TimelineSpan(summary, role)])];
        }

        if (ShouldParseMarkdown(entry.Kind))
        {
            ControlPlaceholderScope scope = CreateControlPlaceholderScope(entry.Text);
            return DecodeControlPlaceholders(
                TerminalMarkdown.Parse(EncodeControlCharacters(entry.Text, scope), role),
                scope);
        }

        return CreateLiteralBodyBlocks(entry.Text, role);
    }

    private static bool ShouldParseMarkdown(ChatEntryKind kind)
    {
        return kind is ChatEntryKind.User or ChatEntryKind.Agent or ChatEntryKind.Thought;
    }

    private static IReadOnlyList<TimelineBlock> CreateLiteralBodyBlocks(string value, TimelineRole role)
    {
        string normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        string[] lines = normalized.Split('\n');
        TimelineBlock[] blocks = new TimelineBlock[lines.Length];
        for (int index = 0; index < lines.Length; index++)
        {
            blocks[index] = new TimelineBlock(
                TimelineBlockKind.Paragraph,
                [new TimelineSpan(lines[index], role)]);
        }

        return Array.AsReadOnly(blocks);
    }

    private static IReadOnlyList<TimelineSpan> GetRenderableSpans(TimelineBlock block)
    {
        List<TimelineSpan> spans = new();
        bool isHeading = block.Kind
            is TimelineBlockKind.Heading1
            or TimelineBlockKind.Heading2
            or TimelineBlockKind.Heading3;

        if (block.Kind == TimelineBlockKind.UnorderedListItem)
        {
            TimelineSpan marker = CreateListMarker(block, "- ");
            spans.Add(marker);
        }
        else if (block.Kind == TimelineBlockKind.OrderedListItem)
        {
            TimelineSpan marker = CreateListMarker(
                block,
                string.Concat(block.Ordinal.GetValueOrDefault().ToString(CultureInfo.InvariantCulture), ". "));
            spans.Add(marker);
        }

        foreach (TimelineSpan span in block.Spans)
        {
            spans.Add(isHeading ? span with { Style = span.Style | TimelineTextStyle.Bold } : span);
        }

        return Array.AsReadOnly(spans.ToArray());
    }

    private static TimelineSpan CreateListMarker(TimelineBlock block, string marker)
    {
        TimelineSpan? firstSpan = block.Spans.Count > 0 ? block.Spans[0] : null;
        TimelineRole markerRole = firstSpan?.Role == TimelineRole.Link
            ? TimelineRole.Primary
            : firstSpan?.Role ?? TimelineRole.Primary;

        return new TimelineSpan(
            marker,
            markerRole);
    }

    private static ControlPlaceholderScope CreateControlPlaceholderScope(string value)
    {
        for (int nonce = 0; ; nonce++)
        {
            string prefix = string.Concat(
                ControlPlaceholderStart,
                "agents-control-",
                nonce.ToString(CultureInfo.InvariantCulture),
                ":");

            if (!value.Contains(prefix, StringComparison.Ordinal))
            {
                return new ControlPlaceholderScope(prefix);
            }
        }
    }

    private static string EncodeControlCharacters(string value, ControlPlaceholderScope scope)
    {
        StringBuilder builder = new(value.Length);

        foreach (char character in value)
        {
            if (character is '\r' or '\n' || !char.IsControl(character))
            {
                builder.Append(character);
                continue;
            }

            builder.Append(scope.Prefix);
            builder.Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
            builder.Append(ControlPlaceholderEnd);
        }

        return builder.ToString();
    }

    private static IReadOnlyList<TimelineBlock> DecodeControlPlaceholders(
        IReadOnlyList<TimelineBlock> blocks,
        ControlPlaceholderScope scope)
    {
        List<TimelineBlock> decodedBlocks = new(blocks.Count);
        foreach (TimelineBlock block in blocks)
        {
            List<TimelineSpan> decodedSpans = new(block.Spans.Count);
            foreach (TimelineSpan span in block.Spans)
            {
                decodedSpans.Add(span with { Text = DecodeControlPlaceholders(span.Text, scope) });
            }

            decodedBlocks.Add(block with { Spans = Array.AsReadOnly(decodedSpans.ToArray()) });
        }

        return Array.AsReadOnly(decodedBlocks.ToArray());
    }

    private static string DecodeControlPlaceholders(string value, ControlPlaceholderScope scope)
    {
        StringBuilder builder = new(value.Length);
        for (int index = 0; index < value.Length; index++)
        {
            int codeStart = index + scope.Prefix.Length;
            if (value.Length - index >= scope.Prefix.Length + 5
                && string.Compare(
                    value,
                    index,
                    scope.Prefix,
                    0,
                    scope.Prefix.Length,
                    StringComparison.Ordinal) == 0
                && value[codeStart + 4] == ControlPlaceholderEnd
                && int.TryParse(
                    value.AsSpan(codeStart, 4),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out int codePoint))
            {
                builder.Append((char)codePoint);
                index += scope.Prefix.Length + 4;
                continue;
            }

            builder.Append(value[index]);
        }

        return builder.ToString();
    }

    private static void WrapParagraph(IReadOnlyList<RenderToken> tokens, int width, List<TimelineLine> lines)
    {
        if (tokens.Count == 0)
        {
            lines.Add(new TimelineLine(string.Empty, []));
            return;
        }

        List<TimelineSpan> current = new();
        int currentWidth = 0;

        void EmitCurrentLine()
        {
            lines.Add(new TimelineLine(
                string.Empty,
                current.Count == 0 ? [] : Array.AsReadOnly(current.ToArray())));
            current = new List<TimelineSpan>();
            currentWidth = 0;
        }

        void EmitTextPieces(
            string text,
            TimelineRole role,
            TimelineTextStyle style,
            Uri? linkTarget)
        {
            foreach (string piece in SliceTextElements(text, width))
            {
                lines.Add(new TimelineLine(string.Empty, [new TimelineSpan(piece, role, style, linkTarget)]));
            }
        }

        void EmitTab(RenderToken token)
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
                Append(
                    current,
                    new string(' ', toWrite),
                    token.Role,
                    token.Style,
                    token.LinkTarget);
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
            int tokenWidth = MeasureDisplayWidth(token.Text);

            if (treatAsControlEscape)
            {
                if (token.LeadingSpace && currentWidth > 0)
                {
                    EmitCurrentLine();
                }

                if (currentWidth > 0)
                {
                    EmitCurrentLine();
                }

                EmitTextPieces(token.Text, token.Role, token.Style, token.LinkTarget);
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
                    Append(
                        current,
                        " ",
                        token.LeadingSpaceRole,
                        token.LeadingSpaceStyle,
                        token.LeadingSpaceLinkTarget);
                    currentWidth++;
                }
            }

            if (tokenWidth > width)
            {
                if (currentWidth > 0)
                {
                    EmitCurrentLine();
                }

                EmitTextPieces(token.Text, token.Role, token.Style, token.LinkTarget);
                return;
            }

            if (currentWidth + tokenWidth > width)
            {
                EmitCurrentLine();
            }

            Append(current, token.Text, token.Role, token.Style, token.LinkTarget);
            currentWidth += tokenWidth;
        }

        foreach (RenderToken token in tokens)
        {
            switch (token.Kind)
            {
                case RenderTokenKind.Tab:
                    EmitTab(token);
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

        if (currentWidth > 0 || current.Count > 0)
        {
            EmitCurrentLine();
        }
    }

    private static IReadOnlyList<RenderToken> TokenizeParagraph(IReadOnlyList<TimelineSpan> spans)
    {
        List<RenderToken> tokens = new();
        StringBuilder current = new();
        bool leadingSpace = false;
        TimelineRole currentRole = TimelineRole.Primary;
        TimelineTextStyle currentStyle = TimelineTextStyle.None;
        Uri? currentLinkTarget = null;
        TimelineRole leadingSpaceRole = TimelineRole.Primary;
        TimelineTextStyle leadingSpaceStyle = TimelineTextStyle.None;
        Uri? leadingSpaceLinkTarget = null;

        void FlushCurrentWord()
        {
            if (current.Length == 0)
            {
                return;
            }

            tokens.Add(new RenderToken(
                RenderTokenKind.Text,
                current.ToString(),
                leadingSpace,
                currentRole,
                currentStyle,
                currentLinkTarget,
                leadingSpaceRole,
                leadingSpaceStyle,
                leadingSpaceLinkTarget));
            current.Clear();
            leadingSpace = false;
        }

        foreach (TimelineSpan span in spans)
        {
            foreach (string textElement in EnumerateTextElements(span.Text))
            {
                if (current.Length > 0
                    && (currentRole != span.Role
                        || currentStyle != span.Style
                        || !Equals(currentLinkTarget, span.LinkTarget)))
                {
                    FlushCurrentWord();
                }

                currentRole = span.Role;
                currentStyle = span.Style;
                currentLinkTarget = span.LinkTarget;

                if (textElement == " ")
                {
                    if (current.Length > 0)
                    {
                        FlushCurrentWord();
                    }

                    leadingSpace = true;
                    leadingSpaceRole = span.Role;
                    leadingSpaceStyle = span.Style;
                    leadingSpaceLinkTarget = span.LinkTarget;
                    continue;
                }

                if (textElement == "\t")
                {
                    FlushCurrentWord();
                    tokens.Add(new RenderToken(
                        RenderTokenKind.Tab,
                        string.Empty,
                        false,
                        span.Role,
                        span.Style,
                        span.LinkTarget,
                        TimelineRole.Primary,
                        TimelineTextStyle.None,
                        null));
                    leadingSpace = false;
                    continue;
                }

                if (textElement.Length == 1 && char.IsControl(textElement[0]))
                {
                    FlushCurrentWord();
                    tokens.Add(new RenderToken(
                        RenderTokenKind.ControlEscape,
                        EscapeControl(textElement[0]),
                        leadingSpace,
                        span.Role,
                        span.Style,
                        span.LinkTarget,
                        leadingSpaceRole,
                        leadingSpaceStyle,
                        leadingSpaceLinkTarget));
                    leadingSpace = false;
                    continue;
                }

                current.Append(textElement);
            }
        }

        FlushCurrentWord();
        return tokens;
    }

    private static void Append(
        List<TimelineSpan> line,
        string text,
        TimelineRole role,
        TimelineTextStyle style,
        Uri? linkTarget)
    {
        if (text.Length == 0)
        {
            return;
        }

        if (line.Count > 0
            && line[^1].Role == role
            && line[^1].Style == style
            && Equals(line[^1].LinkTarget, linkTarget))
        {
            TimelineSpan previous = line[^1];
            line[^1] = previous with { Text = previous.Text + text };
            return;
        }

        line.Add(new TimelineSpan(text, role, style, linkTarget));
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
            column += Math.Max(0, textElement.GetColumns());
        }

        return builder.ToString().Trim();
    }

    private static string TruncateToDisplayWidth(string value, int width)
    {
        int contentWidth = Math.Max(1, width);
        int usedWidth = 0;
        StringBuilder builder = new();

        foreach (string textElement in EnumerateTextElements(value))
        {
            int elementWidth = Math.Max(0, textElement.GetColumns());
            if (elementWidth > contentWidth)
            {
                if (builder.Length == 0)
                {
                    builder.Append('?');
                }

                break;
            }

            if (usedWidth + elementWidth > contentWidth)
            {
                break;
            }

            builder.Append(textElement);
            usedWidth += elementWidth;
        }

        return builder.ToString();
    }

    private static int MeasureDisplayWidth(string value)
    {
        return Math.Max(0, value.GetColumns());
    }

    private static IEnumerable<string> SliceTextElements(string value, int width)
    {
        int contentWidth = Math.Max(1, width);
        int usedWidth = 0;
        StringBuilder builder = new();

        foreach (string textElement in EnumerateTextElements(value))
        {
            int elementWidth = Math.Max(0, textElement.GetColumns());
            if (elementWidth > contentWidth)
            {
                if (builder.Length > 0)
                {
                    yield return builder.ToString();
                    builder.Clear();
                    usedWidth = 0;
                }

                yield return "?";
                continue;
            }

            if (usedWidth > 0 && usedWidth + elementWidth > contentWidth)
            {
                yield return builder.ToString();
                builder.Clear();
                usedWidth = 0;
            }

            builder.Append(textElement);
            usedWidth += elementWidth;
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
