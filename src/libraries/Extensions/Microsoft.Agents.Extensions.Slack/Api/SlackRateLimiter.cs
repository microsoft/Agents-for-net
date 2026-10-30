// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Slack.Api;

internal sealed class SlackRateLimiter(TimeProvider timeProvider)
{
    private sealed class Slot
    {
        internal readonly SemaphoreSlim Gate = new(1, 1);
        internal DateTimeOffset Next;
        internal DateTimeOffset LastUsed;
        internal int Users;
    }

    internal static readonly SlackRateLimiter Shared = new(System.TimeProvider.System);
    private readonly Dictionary<string, Slot> _slots = new(StringComparer.Ordinal);
    private DateTimeOffset _nextCleanup;
    internal TimeProvider TimeProvider { get; } = timeProvider;

    internal async Task WaitAsync(string key, bool pace, CancellationToken cancellationToken)
    {
        Slot slot;
        lock (_slots)
        {
            var now = TimeProvider.GetUtcNow();
            if (_slots.Count >= 128 && now >= _nextCleanup)
            {
                var expired = new List<string>();
                foreach (var pair in _slots)
                {
                    if (pair.Value.Users == 0 && pair.Value.Next <= now
                        && now - pair.Value.LastUsed > TimeSpan.FromMinutes(1))
                    {
                        expired.Add(pair.Key);
                    }
                }
                foreach (var expiredKey in expired)
                {
                    _slots[expiredKey].Gate.Dispose();
                    _slots.Remove(expiredKey);
                }
                _nextCleanup = now.AddMinutes(1);
            }

            if (!_slots.TryGetValue(key, out slot))
            {
                slot = new Slot();
                _slots.Add(key, slot);
            }
            slot.Users++;
            slot.LastUsed = now;
        }

        try
        {
            await slot.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                while (true)
                {
                    TimeSpan delay;
                    lock (_slots)
                    {
                        var now = TimeProvider.GetUtcNow();
                        delay = slot.Next - now;
                        if (delay <= TimeSpan.Zero)
                        {
                            slot.Next = pace ? now.AddSeconds(1) : now;
                            slot.LastUsed = now;
                            return;
                        }
                    }
                    await DelayAsync(delay, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                slot.Gate.Release();
            }
        }
        finally
        {
            lock (_slots)
            {
                slot.Users--;
            }
        }
    }

    internal void Postpone(string key, TimeSpan delay)
    {
        lock (_slots)
        {
            if (!_slots.TryGetValue(key, out var slot))
            {
                slot = new Slot();
                _slots.Add(key, slot);
            }
            var now = TimeProvider.GetUtcNow();
            var next = now + delay;
            if (next > slot.Next)
            {
                slot.Next = next;
            }
            slot.LastUsed = now;
        }
    }

    internal Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        return Task.Delay(delay, TimeProvider, cancellationToken);
#else
        return TimeProvider.Delay(delay, cancellationToken);
#endif
    }
}
