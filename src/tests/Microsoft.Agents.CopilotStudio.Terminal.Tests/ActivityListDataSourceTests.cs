#nullable enable

using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Agents.Core.Models;
using Terminal.Gui.Text;
using Terminal.Gui.Views;

public sealed class ActivityListDataSourceTests
{
    [Fact]
    public void AppendSequence_FormatsEachNewRowOnceWithoutRescanningOldRows()
    {
        ObservableCollection<ActivityRecord> records = [];
        int formatCalls = 0;
        using IListDataSource source = CreateSource(records, record =>
        {
            formatCalls++;
            return record.Summary;
        });

        foreach (int sequence in Enumerable.Range(1, 1_000))
        {
            records.Add(Record(sequence, new string('x', sequence)));
        }

        Assert.Equal(1_000, source.Count);
        Assert.Equal(1_000, formatCalls);
        Assert.Equal(1_000, source.MaxItemLength);
        Assert.Same(records, source.ToList());
    }

    [Fact]
    public void NonAppendChanges_RecomputeRowsAndMaximumLength()
    {
        ObservableCollection<ActivityRecord> records =
        [
            Record(1, "short"),
            Record(2, "the longest row")
        ];
        int formatCalls = 0;
        using IListDataSource source = CreateSource(records, record =>
        {
            formatCalls++;
            return record.Summary;
        });

        Assert.Equal("the longest row".GetColumns(), source.MaxItemLength);
        Assert.Equal(2, formatCalls);

        records.RemoveAt(1);

        Assert.Equal("short".GetColumns(), source.MaxItemLength);
        Assert.Equal(3, formatCalls);

        records[0] = Record(3, "replacement");

        Assert.Equal("replacement".GetColumns(), source.MaxItemLength);
        Assert.Equal(4, formatCalls);

        records.Clear();

        Assert.Equal(0, source.Count);
        Assert.Equal(0, source.MaxItemLength);
    }

    [Fact]
    public void Dispose_UnsubscribesFromRecordChanges()
    {
        ObservableCollection<ActivityRecord> records = [];
        int formatCalls = 0;
        IListDataSource source = CreateSource(records, record =>
        {
            formatCalls++;
            return record.Summary;
        });

        source.Dispose();
        records.Add(Record(1, "after dispose"));

        Assert.Equal(0, formatCalls);
    }

    private static IListDataSource CreateSource(
        ObservableCollection<ActivityRecord> records,
        Func<ActivityRecord, string> formatter)
    {
        return new ActivityListDataSource(records, formatter);
    }

    private static ActivityRecord Record(long sequence, string summary)
    {
        return new ActivityRecord(
            sequence,
            ActivityDirection.Inbound,
            DateTimeOffset.Parse("2026-09-13T12:00:00Z"),
            ActivityTypes.Message,
            summary,
            """{"type":"message"}""",
            null);
    }
}
