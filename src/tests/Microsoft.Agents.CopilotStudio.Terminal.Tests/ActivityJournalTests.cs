using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;

public sealed class ActivityJournalTests
{
    [Fact]
    public void Append_AssignsStableIncreasingSequenceAndDirection()
    {
        ActivityJournal journal = new();
        ActivityRecord outbound = journal.Append(new Activity { Type = "message" }, ActivityDirection.Outbound);
        ActivityRecord inbound = journal.Append(new Activity { Type = "typing" }, ActivityDirection.Inbound);

        Assert.Equal(1, outbound.Sequence);
        Assert.Equal(2, inbound.Sequence);
        Assert.Equal(ActivityDirection.Outbound, outbound.Direction);
        Assert.Equal(ActivityDirection.Inbound, inbound.Direction);
        Assert.Equal([outbound, inbound], journal.Snapshot());
    }

    [Fact]
    public async Task Append_ConcurrentCalls_DoNotDuplicateSequenceNumbers()
    {
        ActivityJournal journal = new();
        await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Task.Run(
            () => journal.Append(new Activity { Type = "event" }, ActivityDirection.Inbound))));

        long[] sequence = journal.Snapshot().Select(record => record.Sequence).Order().ToArray();
        Assert.Equal(Enumerable.Range(1, 100).Select(value => (long)value), sequence);
    }

    [Fact]
    public void Append_SerializationFailure_PreservesActivityAndAddsDiagnostic()
    {
        ActivityJournal journal = new(_ => throw new JsonException("bad payload"));
        List<ActivityRecord> added = [];
        journal.RecordAdded += (_, record) => added.Add(record);

        ActivityRecord activity = journal.Append(
            new Activity { Type = "message", Text = "hello" },
            ActivityDirection.Inbound);

        Assert.Null(activity.Json);
        Assert.Equal(2, added.Count);
        Assert.Equal(ActivityDirection.Inbound, added[0].Direction);
        Assert.Equal(ActivityDirection.Diagnostic, added[1].Direction);
        Assert.Contains("bad payload", added[1].Summary);
    }
}
