// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using Xunit;

namespace Microsoft.Agents.Extensions.Slack.Tests;

public class SlackEventDeduplicatorTests
{
    [Fact]
    public void TryAccept_FirstEventId_ReturnsTrue()
    {
        var deduplicator = new SlackEventDeduplicator();

        Assert.True(deduplicator.TryAccept("Ev123"));
    }

    [Fact]
    public void TryAccept_DuplicateEventId_ReturnsFalse()
    {
        var deduplicator = new SlackEventDeduplicator();

        Assert.True(deduplicator.TryAccept("Ev123"));
        Assert.False(deduplicator.TryAccept("Ev123"));
    }

    [Fact]
    public void Remove_PreviouslyAcceptedEventId_AllowsRetry()
    {
        var deduplicator = new SlackEventDeduplicator();
        Assert.True(deduplicator.TryAccept("Ev123"));

        deduplicator.Remove("Ev123");

        Assert.True(deduplicator.TryAccept("Ev123"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TryAccept_NullOrEmptyEventId_AlwaysReturnsTrue(string? eventId)
    {
        var deduplicator = new SlackEventDeduplicator();

        Assert.True(deduplicator.TryAccept(eventId));
        Assert.True(deduplicator.TryAccept(eventId));
        deduplicator.Remove(eventId);
    }
}
