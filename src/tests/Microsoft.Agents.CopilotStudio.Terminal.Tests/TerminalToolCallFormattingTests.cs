#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

public sealed class TerminalToolCallFormattingTests
{
    [Theory]
    [InlineData(0, "0 ms")]
    [InlineData(842, "842 ms")]
    [InlineData(1000, "1 s")]
    [InlineData(2971, "2.97 s")]
    [InlineData(12400, "12.4 s")]
    [InlineData(59999, "60 s")]
    [InlineData(60000, "1m")]
    [InlineData(133000, "2m 13s")]
    public void FormatDuration_UsesAdaptiveUnits(long durationMs, string expected)
    {
        Assert.Equal(expected, TerminalToolCallFormatting.FormatDuration(durationMs));
    }

    [Fact]
    public void FormatDuration_RoundsAtTheSubMinuteBoundaryBeforeSwitchingUnits()
    {
        Assert.Equal("60 s", TerminalToolCallFormatting.FormatDuration(59999));
        Assert.Equal("1m", TerminalToolCallFormatting.FormatDuration(60000));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1L)]
    public void FormatDuration_OmitsMissingOrNegativeValues(long? durationMs)
    {
        Assert.Null(TerminalToolCallFormatting.FormatDuration(durationMs));
    }

    [Fact]
    public void BuildBlocks_CompletedCallUsesTimelineNarrative()
    {
        ToolCallDetails details = WeatherTool(
            status: "completed",
            durationMs: 2971,
            filled:
            [
                Parameter("Location", "Seattle, WA, USA"),
                Parameter("units", "I")
            ],
            unfilled: []);

        IReadOnlyList<TimelineBlock> blocks = TerminalToolCallFormatting.BuildBlocks(details);

        Assert.Equal(
            [
                "Connector · Get current weather",
                string.Empty,
                "✓ Completed in 2.97 s",
                string.Empty,
                "Called with",
                "Location = Seattle, WA, USA",
                "units = I",
                string.Empty,
                "No parameters were left unfilled."
            ],
            Flatten(blocks));
    }

    [Fact]
    public void BuildBlocks_RunningCallShowsOutstandingParameters()
    {
        ToolCallDetails details = WeatherTool(
            status: "started",
            durationMs: null,
            filled:
            [
                Parameter("query", new { city = "Seattle", units = "metric" }),
                Parameter("includeForecast", true)
            ],
            unfilled: ["date"]);

        IReadOnlyList<TimelineBlock> blocks = TerminalToolCallFormatting.BuildBlocks(details);

        Assert.Equal(
            [
                "Connector · Get current weather",
                string.Empty,
                "◌ Running",
                string.Empty,
                "Calling with",
                "query = {\"city\":\"Seattle\",\"units\":\"metric\"}",
                "includeForecast = true",
                string.Empty,
                "Waiting for",
                "date"
            ],
            Flatten(blocks));
    }

    [Fact]
    public void BuildBlocks_UnknownStatusOmitsMissingSubtitleAndUsesNaturalFallbacks()
    {
        ToolCallDetails details = new(
            "tool-1",
            "current_weather",
            null,
            null,
            "queued",
            [],
            [],
            null);

        IReadOnlyList<TimelineBlock> blocks = TerminalToolCallFormatting.BuildBlocks(details);

        Assert.Equal(
            [
                "Queued",
                string.Empty,
                "Called with no parameters.",
                string.Empty,
                "No unfilled parameters."
            ],
            Flatten(blocks));
    }

    [Fact]
    public void BuildBlocks_RendersStringNullArrayAndObjectValuesLiterally()
    {
        ToolCallDetails details = WeatherTool(
            status: "completed",
            durationMs: null,
            filled:
            [
                Parameter("Location", "Seattle"),
                Parameter("count", 3),
                Parameter("metadata", new { enabled = true }),
                Parameter("tags", new[] { "one", "two" }),
                Parameter("value", null)
            ],
            unfilled: []);

        IReadOnlyList<TimelineBlock> blocks = TerminalToolCallFormatting.BuildBlocks(details);

        Assert.Equal(
            [
                "Location = Seattle",
                "count = 3",
                "metadata = {\"enabled\":true}",
                "tags = [\"one\",\"two\"]",
                "value = null"
            ],
            Flatten(blocks).Where(line => line.Contains(" = ", System.StringComparison.Ordinal)).ToArray());
    }

    [Fact]
    public void BuildBlocks_MultilineStringParametersSplitIntoReadableContinuationParagraphs()
    {
        ToolCallDetails details = WeatherTool(
            status: "completed",
            durationMs: null,
            filled:
            [
                Parameter("notes", "alpha\r\nbeta\ngamma"),
                Parameter("units", "I")
            ],
            unfilled: []);

        IReadOnlyList<TimelineBlock> blocks = TerminalToolCallFormatting.BuildBlocks(details);

        Assert.Equal(
            [
                "notes = alpha",
                "notes = beta",
                "notes = gamma",
                "units = I"
            ],
            Flatten(blocks).Where(line => line.Contains(" = ", System.StringComparison.Ordinal)).ToArray());
    }

    private static string[] Flatten(IReadOnlyList<TimelineBlock> blocks)
    {
        return blocks
            .Select(block => string.Concat(block.Spans.Select(span => span.Text)))
            .ToArray();
    }

    private static ToolCallDetails WeatherTool(
        string status,
        long? durationMs,
        IReadOnlyList<ToolCallParameter> filled,
        IReadOnlyList<string> unfilled)
    {
        return new ToolCallDetails(
            "tool-1",
            "current_weather",
            "Get current weather",
            "Connector",
            status,
            filled,
            unfilled,
            durationMs);
    }

    private static ToolCallParameter Parameter(string name, object? value)
    {
        return new ToolCallParameter(name, JsonSerializer.SerializeToElement(value));
    }
}
