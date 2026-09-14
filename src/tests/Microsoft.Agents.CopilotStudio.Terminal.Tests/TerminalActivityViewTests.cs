#nullable enable

using System;
using System.Linq;
using Microsoft.Agents.Core.Models;
using Terminal.Gui.Views;

[Collection("TerminalGui")]
public sealed class TerminalActivityViewTests
{
    [Theory]
    [InlineData(72, 24, 28, 44)]
    [InlineData(100, 24, 36, 64)]
    public void Layout_SplitsListBesideJsonAtResponsiveBreakpoint(
        int width,
        int height,
        int expectedListWidth,
        int expectedJsonWidth)
    {
#pragma warning disable CS0618 // Task 7 requires Terminal.Gui's TextView for the JSON inspector.
        using TerminalActivityView inspector = CreateInspector(width, height, out ListView<ActivityRecord> list, out TextView json);
#pragma warning restore CS0618

        Assert.Same(list, inspector.ActivityList);
        Assert.Same(json, inspector.Json);
        Assert.Equal(new System.Drawing.Rectangle(0, 0, expectedListWidth, height), list.Frame);
        Assert.Equal(new System.Drawing.Rectangle(expectedListWidth, 0, expectedJsonWidth, height), json.Frame);
    }

    [Theory]
    [InlineData(71, 24, 9, 15)]
    [InlineData(60, 24, 9, 15)]
    [InlineData(30, 8, 3, 5)]
    public void Layout_StacksListAboveJsonBelowResponsiveBreakpoint(
        int width,
        int height,
        int expectedListHeight,
        int expectedJsonHeight)
    {
#pragma warning disable CS0618 // Task 7 requires Terminal.Gui's TextView for the JSON inspector.
        using TerminalActivityView inspector = CreateInspector(width, height, out ListView<ActivityRecord> list, out TextView json);
#pragma warning restore CS0618

        Assert.Equal(new System.Drawing.Rectangle(0, 0, width, expectedListHeight), list.Frame);
        Assert.Equal(new System.Drawing.Rectangle(0, expectedListHeight, width, expectedJsonHeight), json.Frame);
    }

    [Fact]
    public void Inspector_PreservesReadOnlyScrollableJsonConfiguration()
    {
#pragma warning disable CS0618 // Task 7 requires Terminal.Gui's TextView for the JSON inspector.
        using TerminalActivityView inspector = CreateInspector(60, 24, out ListView<ActivityRecord> list, out TextView json);
#pragma warning restore CS0618

        Assert.Same(list, Assert.Single(inspector.SubViews.OfType<ListView<ActivityRecord>>()));
#pragma warning disable CS0618 // Task 7 requires Terminal.Gui's TextView for the JSON inspector.
        Assert.Same(json, Assert.Single(inspector.SubViews.OfType<TextView>()));
#pragma warning restore CS0618
        Assert.True(json.ReadOnly);
        Assert.True(json.ScrollBars);
        Assert.False(json.WordWrap);
    }

#pragma warning disable CS0618 // Task 7 requires Terminal.Gui's TextView for the JSON inspector.
    private static TerminalActivityView CreateInspector(
        int width,
        int height,
        out ListView<ActivityRecord> list,
        out TextView json)
    {
        list = new ListView<ActivityRecord>();
        list.SetSource(
        [
            new ActivityRecord(
                1,
                ActivityDirection.Inbound,
                DateTimeOffset.Parse("2026-09-13T12:00:00Z"),
                ActivityTypes.Message,
                "A long activity summary that should clip at the viewport edge instead of collapsing.",
                null,
                """{"type":"message"}""",
                null)
        ]);
#pragma warning disable CS0618 // Task 7 requires Terminal.Gui's TextView for the JSON inspector.
        json = new TextView
        {
            ReadOnly = true,
            ScrollBars = true,
            WordWrap = false,
            Text = """{"type":"message"}"""
        };
#pragma warning restore CS0618

        TerminalActivityView inspector = new(
            list,
            json,
            TerminalPalette.Create(null, supportsTrueColor: false));
        inspector.Frame = new System.Drawing.Rectangle(0, 0, width, height);
        Assert.True(inspector.Layout());
        return inspector;
    }
#pragma warning restore CS0618
}
