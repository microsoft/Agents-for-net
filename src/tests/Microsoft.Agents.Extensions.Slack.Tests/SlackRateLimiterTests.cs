// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Slack.Api;
using Microsoft.Extensions.Time.Testing;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.Slack.Tests;

public class SlackRateLimiterTests
{
    [Fact]
    public async Task SharedBudget_RestrictsSameMethodWithoutBlockingOtherScopes()
    {
        var clock = new FakeTimeProvider();
        var limiter = new SlackRateLimiter(clock);
        await limiter.WaitAsync("app:team:append", true, CancellationToken.None);
        var waiting = limiter.WaitAsync("app:team:append", true, CancellationToken.None);

        Assert.False(waiting.IsCompleted);
        await limiter.WaitAsync("app:team:stop", true, CancellationToken.None);
        await limiter.WaitAsync("app:other-team:append", true, CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(1));

        await waiting.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task RetryAfter_ExtendsAnAlreadyWaitingRequestsDeadline()
    {
        var clock = new FakeTimeProvider();
        var limiter = new SlackRateLimiter(clock);
        await limiter.WaitAsync("app:team:append", true, CancellationToken.None);
        var waiting = limiter.WaitAsync("app:team:append", true, CancellationToken.None);
        limiter.Postpone("app:team:append", TimeSpan.FromSeconds(5));

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.False(waiting.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(4));

        await waiting.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task CancelledWait_DoesNotBlockTheNextRequest()
    {
        var clock = new FakeTimeProvider();
        var limiter = new SlackRateLimiter(clock);
        await limiter.WaitAsync("app:team:append", true, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var waiting = limiter.WaitAsync("app:team:append", true, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);

        clock.Advance(TimeSpan.FromSeconds(1));
        await limiter.WaitAsync("app:team:append", true, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
    }
}
