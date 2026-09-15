#nullable enable

using System;
using Microsoft.Agents.Core.Models;

public sealed class TerminalActivityStateTests
{
    [Fact]
    public void Add_FollowsLatestUntilUserSelectsAnOlderRecord()
    {
        TerminalActivityState state = new();
        ActivityRecord first = Record(1, "one");
        ActivityRecord second = Record(2, "two");
        ActivityRecord third = Record(3, "three");

        state.Add(first);
        state.Add(second);
        state.Select(first.Sequence);
        state.Add(third);

        Assert.Equal(3, state.Records.Count);
        Assert.Same(first, state.Selected);
        Assert.Equal("one", state.SelectedText);
    }

    [Fact]
    public void SelectedText_FormatsOnlyCurrentSelectionAndCachesIt()
    {
        int formatCalls = 0;
        TerminalActivityState state = CreateState(json =>
        {
            formatCalls++;
            return $"pretty:{json}";
        });
        ActivityRecord withJson = Record(1, "one", """{"type":"message"}""");
        ActivityRecord second = Record(2, "two", """{"type":"event"}""");

        state.Add(withJson);
        state.Add(second);

        Assert.Equal(0, formatCalls);
        Assert.Equal("""pretty:{"type":"event"}""", state.SelectedText);
        Assert.Equal("""pretty:{"type":"event"}""", state.SelectedText);
        Assert.Equal(1, formatCalls);

        state.Select(withJson.Sequence);

        Assert.Equal(1, formatCalls);
        Assert.Equal("""pretty:{"type":"message"}""", state.SelectedText);
        Assert.Equal(2, formatCalls);
    }

    [Fact]
    public void SelectedText_UsesSummaryWithoutCallingFormatterWhenJsonIsUnavailable()
    {
        int formatCalls = 0;
        TerminalActivityState state = CreateState(_ =>
        {
            formatCalls++;
            return "unexpected";
        });
        ActivityRecord diagnostic = Record(1, "diagnostic");

        state.Add(diagnostic);

        Assert.Equal("diagnostic", state.SelectedText);
        Assert.Equal(0, formatCalls);
    }

    [Fact]
    public void Select_ResolvesTheJournalRecordBySequence()
    {
        TerminalActivityState state = new();
        ActivityRecord canonical = Record(7, "canonical");
        state.Add(canonical);

        state.Select(7);

        Assert.Same(canonical, state.Selected);
    }

    private static ActivityRecord Record(long sequence, string summary, string? json = null)
    {
        return new ActivityRecord(
            sequence,
            ActivityDirection.Inbound,
            DateTimeOffset.Parse("2026-09-11T12:00:00Z"),
            ActivityTypes.Message,
            summary,
            json,
            null);
    }

    private static TerminalActivityState CreateState(Func<string, string> formatter)
    {
        return new TerminalActivityState(formatter);
    }
}
