#nullable enable

using System;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Terminal.Gui.Drawing;

#pragma warning disable CS0618 // Task 7 explicitly requires TextView for the JSON inspector.
internal sealed class TerminalActivityView : View
{
    private const int ResponsiveStackBreakpoint = 72;
    private const int MinimumWideListWidth = 28;
    private const int MinimumStackedListHeight = 3;

    internal TerminalActivityView(
        ListView activityList,
        TextView json,
        TerminalPalette palette,
        Action requestFullRefresh)
    {
        ActivityList = activityList ?? throw new ArgumentNullException(nameof(activityList));
        Json = json ?? throw new ArgumentNullException(nameof(json));
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentNullException.ThrowIfNull(requestFullRefresh);

        ActivityList.SetScheme(palette.CreateActivityListScheme());
        Json.SetScheme(palette.CreateControlScheme());

        Width = Dim.Fill();
        Height = Dim.Fill();
        CanFocus = true;
        BorderStyle = LineStyle.None;

        Add(ActivityList, Json);
        ActivityList.ValueChanged += (_, _) =>
        {
            SetNeedsDraw();
            requestFullRefresh();
        };
        ActivityList.ViewportChanged += (_, _) =>
        {
            SetNeedsDraw();
            requestFullRefresh();
        };
    }

    internal ListView ActivityList { get; }

    internal TextView Json { get; }

    protected override void OnSubViewLayout(LayoutEventArgs args)
    {
        base.OnSubViewLayout(args);

        int width = Math.Max(0, Viewport.Width > 0 ? Viewport.Width : Frame.Width);
        int height = Math.Max(0, Viewport.Height > 0 ? Viewport.Height : Frame.Height);
        if (width < ResponsiveStackBreakpoint)
        {
            int listHeight = GetStackedListHeight(height);
            SetFrameAndLayout(ActivityList, 0, 0, width, listHeight);
            SetFrameAndLayout(Json, 0, listHeight, width, Math.Max(0, height - listHeight));
            return;
        }

        int listWidth = GetWideListWidth(width);
        SetFrameAndLayout(ActivityList, 0, 0, listWidth, height);
        SetFrameAndLayout(Json, listWidth, 0, Math.Max(0, width - listWidth), height);
    }

    private static int GetWideListWidth(int width)
    {
        if (width <= 1)
        {
            return width;
        }

        int preferredWidth = Math.Max(MinimumWideListWidth, (int)Math.Floor(width * 0.36));
        return Math.Min(width - 1, preferredWidth);
    }

    private static int GetStackedListHeight(int height)
    {
        if (height <= MinimumStackedListHeight)
        {
            return height;
        }

        int maximumListHeight = Math.Max(MinimumStackedListHeight, (int)Math.Floor(height * 0.4));
        return Math.Min(height - 1, maximumListHeight);
    }

    private static void SetFrameAndLayout(View view, int x, int y, int width, int height)
    {
        view.Frame = new System.Drawing.Rectangle(x, y, width, height);
        view.Layout();
    }
}
#pragma warning restore CS0618
