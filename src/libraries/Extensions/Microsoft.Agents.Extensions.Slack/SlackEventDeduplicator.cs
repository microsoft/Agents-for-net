// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using System;
using System.Collections.Concurrent;

namespace Microsoft.Agents.Extensions.Slack
{
    internal sealed class SlackEventDeduplicator
    {
        private static readonly TimeSpan Retention = TimeSpan.FromMinutes(10);
        private const int MaxEntries = 5000;

        private readonly ConcurrentDictionary<string, DateTimeOffset> _processedEvents = new();

        public bool TryAccept(string? eventId)
        {
            if (string.IsNullOrEmpty(eventId))
            {
                return true;
            }

            Prune();
            return _processedEvents.TryAdd(eventId, DateTimeOffset.UtcNow);
        }

        public void Remove(string? eventId)
        {
            if (!string.IsNullOrEmpty(eventId))
            {
                _processedEvents.TryRemove(eventId, out _);
            }
        }

        private void Prune()
        {
            var cutoff = DateTimeOffset.UtcNow - Retention;
            foreach (var entry in _processedEvents)
            {
                if (entry.Value < cutoff)
                {
                    _processedEvents.TryRemove(entry.Key, out _);
                }
            }

            if (_processedEvents.Count > MaxEntries)
            {
                foreach (var entry in _processedEvents)
                {
                    _processedEvents.TryRemove(entry.Key, out _);
                    if (_processedEvents.Count <= MaxEntries)
                    {
                        break;
                    }
                }
            }
        }
    }
}
