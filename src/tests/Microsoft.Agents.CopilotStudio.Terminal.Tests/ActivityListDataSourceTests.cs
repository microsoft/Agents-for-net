#nullable enable

using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Agents.Core.Models;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Drawing;
using Terminal.Gui.Text;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

[Collection("TerminalGui")]
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

    [Fact]
    public void Render_FillsTrailingCellsAcrossProvidedWidth()
    {
        string[] cells = RenderRow("abc", width: 6);

        Assert.Equal(["a", "b", "c", " ", " ", " "], cells);
    }

    
    [Theory]
    [MemberData(nameof(HorizontalViewportCases))]
    public void Render_HorizontalViewportUsesDisplayColumnsWithoutSplittingGraphemes(
        string text,
        int viewportX,
        string[] expected)
    {
        string[] cells = RenderRow(text, expected.Length, viewportX, prefill: false);

        Assert.Equal(expected, cells);
    }

    public static TheoryData<string, int, string[]> HorizontalViewportCases =>
        new()
        {
            { "A界B", 1, ["界", " ", "B", " "] },
            { "A界B", 2, [" ", "B", " ", " "] },
//            { "Ae\u0301B", 2, ["B", " ", " ", " "] },
            { "A😀B", 1, ["😀", " ", "B", " "] },
            { "A😀B", 2, [" ", "B", " ", " "] }
        };

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

    private static string[] RenderRow(
        string text,
        int width,
        int viewportX = 0,
        bool prefill = true)
    {
        using IApplication application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        ObservableCollection<ActivityRecord> records = [Record(1, text)];
        using ActivityListDataSource source = new(records, record => record.Summary);
        using ListView list = new()
        {
            Width = width,
            Height = 1
        };
        using Window host = new()
        {
            Width = width,
            Height = 1,
            BorderStyle = LineStyle.None
        };
        host.Add(list);
        application.StopAfterFirstIteration = true;
        application.Run(host);
        if (prefill)
        {
            list.Move(0, 0);
            list.AddStr(new string('x', width));
        }

        source.Render(
            list,
            selected: false,
            item: 0,
            col: 0,
            row: 0,
            width,
            viewportX);
        Cell[,] contents = Assert.IsAssignableFrom<IDriver>(application.Driver).Contents!;
        return Enumerable.Range(0, width)
            .Select(column => contents[0, column].Grapheme)
            .ToArray();
    }
}
