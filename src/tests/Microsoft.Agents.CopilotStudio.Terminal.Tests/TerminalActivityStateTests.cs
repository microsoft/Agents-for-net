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
    public void Select_UsesJsonAndFallsBackToSummary()
    {
        TerminalActivityState state = new();
        ActivityRecord withJson = Record(1, "one", """{"type":"message"}""");
        ActivityRecord summaryOnly = Record(2, "diagnostic");

        state.Add(withJson);
        Assert.Equal("""{"type":"message"}""", state.SelectedText);

        state.Add(summaryOnly);
        Assert.Equal("diagnostic", state.SelectedText);
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
            null,
            json,
            null);
    }
}
