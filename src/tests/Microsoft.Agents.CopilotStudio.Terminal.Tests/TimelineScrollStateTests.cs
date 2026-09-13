#nullable enable

public sealed class TimelineScrollStateTests
{
    [Fact]
    public void SetDimensions_StartsFollowingLatestAtBottom()
    {
        TimelineScrollState state = new();

        state.SetDimensions(contentHeight: 20, viewportHeight: 10);

        Assert.Equal(10, state.Offset);
        Assert.Equal(10, state.MaximumOffset);
        Assert.True(state.IsFollowingLatest);
    }

    [Fact]
    public void SetDimensions_FollowsNewBottomWhenAlreadyFollowing()
    {
        TimelineScrollState state = new();
        state.SetDimensions(contentHeight: 20, viewportHeight: 10);
        state.ScrollToEnd();

        state.SetDimensions(contentHeight: 25, viewportHeight: 10);

        Assert.Equal(15, state.Offset);
        Assert.True(state.IsFollowingLatest);
    }

    [Fact]
    public void SetDimensions_PreservesManualScrollWhenContentGrows()
    {
        TimelineScrollState state = new();
        state.SetDimensions(contentHeight: 20, viewportHeight: 10);
        state.ScrollToEnd();
        state.ScrollBy(-4);

        state.SetDimensions(contentHeight: 25, viewportHeight: 10);

        Assert.Equal(6, state.Offset);
        Assert.False(state.IsFollowingLatest);
    }

    [Theory]
    [InlineData(-100, 0)]
    [InlineData(100, 10)]
    [InlineData(int.MinValue, 0)]
    [InlineData(int.MaxValue, 10)]
    public void ScrollBy_ClampsToValidRange(int delta, int expected)
    {
        TimelineScrollState state = new();
        state.SetDimensions(contentHeight: 20, viewportHeight: 10);

        state.ScrollBy(delta);

        Assert.Equal(expected, state.Offset);
    }
}
