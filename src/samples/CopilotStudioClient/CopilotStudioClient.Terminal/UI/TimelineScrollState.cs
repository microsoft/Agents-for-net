#nullable enable

internal sealed class TimelineScrollState
{
    private bool isFollowingLatest = true;

    internal int Offset { get; private set; }

    internal int MaximumOffset { get; private set; }

    internal bool IsFollowingLatest => isFollowingLatest;

    internal void SetDimensions(int contentHeight, int viewportHeight)
    {
        bool wasFollowingLatest = isFollowingLatest;

        int clampedContentHeight = Math.Max(0, contentHeight);
        int clampedViewportHeight = Math.Max(0, viewportHeight);
        MaximumOffset = Math.Max(0, clampedContentHeight - clampedViewportHeight);

        if (wasFollowingLatest)
        {
            Offset = MaximumOffset;
            isFollowingLatest = true;
            return;
        }

        Offset = Math.Min(Offset, MaximumOffset);
        isFollowingLatest = Offset == MaximumOffset;
    }

    internal void ScrollBy(int delta)
    {
        Offset = ClampOffset((long)Offset + delta);
        isFollowingLatest = Offset == MaximumOffset;
    }

    internal void ScrollToStart()
    {
        Offset = 0;
        isFollowingLatest = false;
    }

    internal void ScrollToEnd()
    {
        Offset = MaximumOffset;
        isFollowingLatest = true;
    }

    private int ClampOffset(long offset)
    {
        return (int)Math.Min((long)MaximumOffset, Math.Max(0L, offset));
    }
}
