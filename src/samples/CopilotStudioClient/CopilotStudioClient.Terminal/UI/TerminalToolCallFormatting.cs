#nullable enable

using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

internal static class TerminalToolCallFormatting
{
    internal static IReadOnlyList<TimelineBlock> BuildBlocks(ToolCallDetails details)
    {
        return BuildBlocks(details, TimelineGlyphSet.Unicode);
    }

    internal static string? FormatDuration(long? durationMs)
    {
        if (durationMs is null || durationMs < 0)
        {
            return null;
        }

        long milliseconds = durationMs.Value;
        if (milliseconds < 1000)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{milliseconds} ms");
        }

        if (milliseconds < 60000)
        {
            decimal seconds = milliseconds / 1000m;
            return string.Concat(
                seconds.ToString("0.##", CultureInfo.InvariantCulture),
                " s");
        }

        long minutes = milliseconds / 60000;
        long secondsRemainder = milliseconds % 60000 / 1000;
        return secondsRemainder == 0
            ? string.Create(CultureInfo.InvariantCulture, $"{minutes}m")
            : string.Create(CultureInfo.InvariantCulture, $"{minutes}m {secondsRemainder}s");
    }

    internal static IReadOnlyList<TimelineBlock> BuildBlocks(ToolCallDetails details, TimelineGlyphSet glyphs)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(glyphs);

        List<TimelineBlock> blocks = [];
        string? subtitle = BuildSubtitle(details, glyphs);
        string status = details.Status.Trim();
        bool isCompleted = string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase);
        bool isRunning = string.Equals(status, "started", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "running", StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(subtitle))
        {
            blocks.Add(Paragraph(new TimelineSpan(subtitle, TimelineRole.Muted)));
            blocks.Add(Paragraph());
        }

        blocks.Add(BuildStatusBlock(status, isCompleted, isRunning, details.DurationMs, glyphs));
        blocks.Add(Paragraph());

        if (details.FilledParameters.Count == 0)
        {
            blocks.Add(Paragraph(new TimelineSpan(
                isRunning ? "Calling with no parameters." : "Called with no parameters.",
                TimelineRole.Muted)));
        }
        else
        {
            blocks.Add(Paragraph(new TimelineSpan(
                isRunning ? "Calling with" : "Called with",
                TimelineRole.Muted)));
            foreach (ToolCallParameter parameter in details.FilledParameters)
            {
                blocks.Add(Paragraph(
                    new TimelineSpan(parameter.Name, TimelineRole.User),
                    new TimelineSpan(" = ", TimelineRole.Muted),
                    new TimelineSpan(FormatParameterValue(parameter.Value), TimelineRole.Primary)));
            }
        }

        blocks.Add(Paragraph());

        if (details.UnfilledParameters.Count == 0)
        {
            string noneText = isRunning
                ? "No parameters are waiting to be filled."
                : isCompleted
                    ? "No parameters were left unfilled."
                    : "No unfilled parameters.";
            blocks.Add(Paragraph(new TimelineSpan(noneText, TimelineRole.Muted)));
        }
        else
        {
            blocks.Add(Paragraph(new TimelineSpan(
                isRunning ? "Waiting for" : "Unfilled parameters",
                TimelineRole.Muted)));
            foreach (string parameter in details.UnfilledParameters)
            {
                blocks.Add(Paragraph(new TimelineSpan(parameter, TimelineRole.Primary)));
            }
        }

        return Array.AsReadOnly(blocks.ToArray());
    }

    private static TimelineBlock BuildStatusBlock(
        string status,
        bool isCompleted,
        bool isRunning,
        long? durationMs,
        TimelineGlyphSet glyphs)
    {
        List<TimelineSpan> spans = [];
        if (isCompleted)
        {
            spans.Add(new TimelineSpan(
                glyphs == TimelineGlyphSet.Ascii ? "Completed" : "✓ Completed",
                TimelineRole.Agent,
                TimelineTextStyle.Bold));
            string? duration = FormatDuration(durationMs);
            if (!string.IsNullOrEmpty(duration))
            {
                spans.Add(new TimelineSpan($" in {duration}", TimelineRole.Muted));
            }

            return Paragraph(spans.ToArray());
        }

        if (isRunning)
        {
            return Paragraph(new TimelineSpan(
                glyphs == TimelineGlyphSet.Ascii ? "Running" : "◌ Running",
                TimelineRole.Muted,
                TimelineTextStyle.Bold));
        }

        return Paragraph(new TimelineSpan(FormatStatusLabel(status), TimelineRole.Muted, TimelineTextStyle.Bold));
    }

    private static string? BuildSubtitle(ToolCallDetails details, TimelineGlyphSet glyphs)
    {
        if (!string.IsNullOrWhiteSpace(details.Category) && !string.IsNullOrWhiteSpace(details.DisplayName))
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{details.Category!.Trim()} {glyphs.DetailSeparator} {details.DisplayName!.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(details.Category))
        {
            return details.Category.Trim();
        }

        if (!string.IsNullOrWhiteSpace(details.DisplayName))
        {
            return details.DisplayName.Trim();
        }

        return null;
    }

    private static string FormatStatusLabel(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "Unknown";
        }

        string trimmed = status.Trim();
        return char.ToUpperInvariant(trimmed[0]) + trimmed[1..];
    }

    private static string FormatParameterValue(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : JsonSerializer.Serialize(value);
    }

    private static TimelineBlock Paragraph(params TimelineSpan[] spans)
    {
        return new TimelineBlock(TimelineBlockKind.Paragraph, Array.AsReadOnly(spans));
    }
}
