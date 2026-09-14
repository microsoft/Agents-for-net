#nullable enable

using System;
using System.Linq;
using System.Reflection;
using Microsoft.Agents.Core.Models;
using Terminal.Gui.Drawing;
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

    [Theory]
    [InlineData("#c9d1d9", "#0d1117", "#f2cc60")]
    [InlineData("#24292f", "#ffffff", "#0969da")]
    public void Inspector_UsesAdaptiveUserColorForSelectedActivity(
        string foreground,
        string background,
        string expectedSelection)
    {
        TerminalPalette palette = TerminalPalette.Create(
            new Terminal.Gui.Drawing.Attribute(
                new Color(foreground),
                new Color(background)),
            supportsTrueColor: true);
#pragma warning disable CS0618 // Task 7 requires Terminal.Gui's TextView for the JSON inspector.
        using TerminalActivityView inspector = CreateInspector(
            60,
            24,
            out ListView<ActivityRecord> list,
            out _,
            palette);
#pragma warning restore CS0618

        Scheme scheme = list.GetScheme();
        Assert.Equal(new Color(expectedSelection), scheme.Focus.Foreground);
        Assert.Equal(new Color(expectedSelection), scheme.Active.Foreground);
        Assert.True(scheme.Focus.Style.HasFlag(TextStyle.Bold));
        Assert.True(scheme.Active.Style.HasFlag(TextStyle.Bold));
        Assert.NotEqual(scheme.Focus, inspector.Json.GetScheme().Focus);
    }

    [Fact]
    public void Inspector_UsesYellowForSelectedActivityInDarkLimitedColorPalette()
    {
        TerminalPalette palette = TerminalPalette.Create(
            new Terminal.Gui.Drawing.Attribute(ColorName16.White, ColorName16.Black),
            supportsTrueColor: false);
#pragma warning disable CS0618 // Task 7 requires Terminal.Gui's TextView for the JSON inspector.
        using TerminalActivityView inspector = CreateInspector(
            60,
            24,
            out ListView<ActivityRecord> list,
            out _,
            palette);
#pragma warning restore CS0618

        Scheme scheme = list.GetScheme();
        Assert.Equal(ColorName16.Yellow, scheme.Focus.Foreground);
        Assert.Equal(ColorName16.Yellow, scheme.Active.Foreground);
        Assert.True(scheme.Focus.Style.HasFlag(TextStyle.Bold));
        Assert.True(scheme.Active.Style.HasFlag(TextStyle.Bold));
    }

    [Fact]
    public void SelectionChange_InvalidatesInspectorAndRequestsFullRefresh()
    {
        int refreshRequests = 0;
#pragma warning disable CS0618 // Task 7 requires Terminal.Gui's TextView for the JSON inspector.
        using TerminalActivityView inspector = CreateInspector(
            60,
            24,
            out ListView<ActivityRecord> list,
            out _,
            requestFullRefresh: () => refreshRequests++);
#pragma warning restore CS0618
        ActivityRecord second = CreateActivity(2);
        list.SetSource([CreateActivity(1), second]);
        ClearNeedsDraw(inspector);
        int requestsBeforeSelection = refreshRequests;

        list.Value = second;

        Assert.True(inspector.NeedsDraw);
        Assert.True(refreshRequests > requestsBeforeSelection);
    }

    [Fact]
    public void ViewportScroll_RequestsFullRefreshWithoutChangingSelection()
    {
        int refreshRequests = 0;
#pragma warning disable CS0618 // Task 7 requires Terminal.Gui's TextView for the JSON inspector.
        using TerminalActivityView inspector = CreateInspector(
            60,
            24,
            out ListView<ActivityRecord> list,
            out _,
            requestFullRefresh: () => refreshRequests++);
#pragma warning restore CS0618
        ActivityRecord[] activities = Enumerable.Range(1, 20)
            .Select(sequence => CreateActivity(sequence))
            .ToArray();
        list.SetSource([.. activities]);
        list.Value = activities[0];
        ActivityRecord? selected = list.Value;
        int requestsBeforeScroll = refreshRequests;

        Assert.True(list.ScrollVertical(1));

        Assert.Same(selected, list.Value);
        Assert.Equal(requestsBeforeScroll + 1, refreshRequests);
    }

#pragma warning disable CS0618 // Task 7 requires Terminal.Gui's TextView for the JSON inspector.
    private static TerminalActivityView CreateInspector(
        int width,
        int height,
        out ListView<ActivityRecord> list,
        out TextView json,
        TerminalPalette? palette = null,
        Action? requestFullRefresh = null)
    {
        list = new ListView<ActivityRecord>();
        list.SetSource(
        [
            CreateActivity(1)
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
            palette ?? TerminalPalette.Create(null, supportsTrueColor: false),
            requestFullRefresh ?? (() => { }));
        inspector.Frame = new System.Drawing.Rectangle(0, 0, width, height);
        Assert.True(inspector.Layout());
        return inspector;
    }

    private static ActivityRecord CreateActivity(long sequence)
    {
        return new ActivityRecord(
            sequence,
            ActivityDirection.Inbound,
            DateTimeOffset.Parse("2026-09-13T12:00:00Z"),
            ActivityTypes.Message,
            "A long activity summary that should clip at the viewport edge instead of collapsing.",
            null,
            """{"type":"message"}""",
            null);
    }

    private static void ClearNeedsDraw(TerminalActivityView inspector)
    {
        typeof(Terminal.Gui.ViewBase.View)
            .GetMethod("ClearNeedsDraw", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(inspector, null);
    }
#pragma warning restore CS0618
}
