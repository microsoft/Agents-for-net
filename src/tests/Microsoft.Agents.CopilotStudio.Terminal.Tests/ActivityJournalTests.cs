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
    public void Append_SnapshotsActivityBeforeCallerMutatesIt()
    {
        Activity activity = new()
        {
            Type = ActivityTypes.Message,
            Text = "hello",
            Entities = [new Entity("custom") { Properties = { ["answer"] = JsonSerializer.SerializeToElement(42) } }]
        };

        ActivityJournal journal = new();
        List<ActivityRecord> added = [];
        journal.RecordAdded += (_, record) => added.Add(record);
        ActivityRecord record = journal.Append(activity, ActivityDirection.Inbound);

        activity.Text = "changed";
        activity.Entities[0].Properties["answer"] = JsonSerializer.SerializeToElement(99);

        ActivityRecord snapshot = journal.Snapshot()[0];

        Assert.NotSame(activity, record.Activity);
        Assert.Equal("hello", record.Activity!.Text);
        Assert.Equal(42, record.Activity.Entities[0].Properties["answer"].GetInt32());
        Assert.Equal("hello", record.Summary);
        Assert.Equal("hello", added[0].Activity!.Text);
        Assert.Equal(42, added[0].Activity.Entities[0].Properties["answer"].GetInt32());
        Assert.Equal("hello", snapshot.Activity!.Text);
        Assert.Equal(42, snapshot.Activity.Entities[0].Properties["answer"].GetInt32());
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

    [Fact]
    public void Append_UnsupportedSnapshotPayload_EmitsActivityAndDiagnosticWithoutThrowing()
    {
        Activity activity = new()
        {
            Type = ActivityTypes.Message,
            Text = "hello"
        };

        ActivityJournal journal = new(cloner: _ => throw new JsonException("bad payload"));
        List<ActivityRecord> added = [];
        journal.RecordAdded += (_, record) => added.Add(record);

        ActivityRecord record = journal.Append(activity, ActivityDirection.Inbound);

        activity.Text = "changed";

        Assert.Equal("message", record.Type);
        Assert.Equal("hello", record.Summary);
        Assert.Null(record.Activity);
        Assert.Equal(2, added.Count);
        Assert.Equal("hello", added[0].Summary);
        Assert.Null(added[0].Activity);
        Assert.Equal(ActivityDirection.Diagnostic, added[1].Direction);
        Assert.Contains("Unable to snapshot activity JSON", added[1].Summary);
        Assert.Single(journal.Snapshot(), item => item.Direction == ActivityDirection.Inbound);
    }
}
