#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

[Collection("TerminalGui")]
public sealed class TerminalNavigationViewTests
{
    [Fact]
    public void GetSpans_MarksOnlyTheActiveSurface()
    {
        using TerminalNavigationView view = new()
        {
            Width = 80,
            Height = 1,
            Palette = LimitedPalette(),
            ActiveSurface = TerminalSurface.Activities
        };

        IReadOnlyList<TimelineSpan> spans = view.GetSpans();

        TimelineSpan active = Assert.Single(
            spans,
            span => span.Text.Contains("Activities", StringComparison.Ordinal));
        Assert.Equal(TimelineRole.ActiveNavigation, active.Role);
        Assert.True(active.Style.HasFlag(TimelineTextStyle.Underline));

        Assert.Contains(spans, span => span.Text == "F1" && span.Role == TimelineRole.Link);
        Assert.Contains(spans, span => span.Text == "Chat" && span.Role == TimelineRole.Muted);
        Assert.Contains(spans, span => span.Text == "F2" && span.Role == TimelineRole.Link);
        Assert.Contains(spans, span => span.Text == "Thoughts" && span.Role == TimelineRole.Muted);
        Assert.Contains(spans, span => span.Text == "F4" && span.Role == TimelineRole.Link);
        Assert.Contains(spans, span => span.Text == "Help" && span.Role == TimelineRole.Muted);
    }

    private static TerminalPalette LimitedPalette() => TerminalPalette.Create(null, supportsTrueColor: false);
}
