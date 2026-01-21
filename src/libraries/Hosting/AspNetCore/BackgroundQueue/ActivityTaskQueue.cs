// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue
{
    /// <summary>
    /// Singleton queue, used to transfer an ActivityWithClaims to the <see cref="HostedActivityService"/>.
    /// </summary>
    internal class ActivityTaskQueue : IActivityTaskQueue, IDisposable
    {
        private readonly ConcurrentQueue<ActivityWithClaims> _activities = new();
        private readonly Subject<Unit> _activitySignal = new();
        private readonly TaskCompletionSource _drainComplete = new();
        private int _stopped = 0;  // Use int for Interlocked operations (0 = not stopped, 1 = stopped)
        private int _pending = 0;  // Track pending activities
        private bool _disposed;

        /// <inheritdoc/>
        public bool QueueBackgroundActivity(ClaimsIdentity claimsIdentity, IChannelAdapter adapter, IActivity activity, bool proactive = false, string proactiveAudience = null, Type agentType = null, Func<InvokeResponse, Task> onComplete = null, IHeaderDictionary headers = null)
        {
            ArgumentNullException.ThrowIfNull(claimsIdentity);
            ArgumentNullException.ThrowIfNull(adapter);
            ArgumentNullException.ThrowIfNull(activity);

            if (Interlocked.CompareExchange(ref _stopped, 0, 0) == 1)
            {
                return false;
            }

            // Increment pending count
            Interlocked.Increment(ref _pending);

            // Copy to prevent unexpected side effects from later mutations of the original headers.
            var copyHeaders = headers != null ? new HeaderDictionary(headers.ToDictionary()) : [];

            _activities.Enqueue(new ActivityWithClaims
            {
                ChannelAdapter = adapter,
                AgentType = agentType,
                ClaimsIdentity = claimsIdentity,
                Activity = activity,
                IsProactive = proactive,
                ProactiveAudience = proactiveAudience,
                OnComplete = onComplete,
                Headers = copyHeaders
            });

            _activitySignal.OnNext(Unit.Default);
            return true;
        }

        /// <inheritdoc/>
        public async Task<ActivityWithClaims> WaitForActivityAsync(CancellationToken cancellationToken)
        {
            // Try to dequeue immediately if items are available
            if (_activities.TryDequeue(out var activity))
            {
                DecrementPendingAndCheckDrain();
                return activity;
            }

            // Wait for a signal that an activity was enqueued
            await _activitySignal.FirstAsync().ToTask(cancellationToken).ConfigureAwait(false);

            _activities.TryDequeue(out activity);
            if (activity != null)
            {
                DecrementPendingAndCheckDrain();
            }

            return activity;
        }

        private void DecrementPendingAndCheckDrain()
        {
            if (Interlocked.Decrement(ref _pending) == 0 &&
                Interlocked.CompareExchange(ref _stopped, 0, 0) == 1)
            {
                _drainComplete.TrySetResult();
            }
        }

        public void Stop(bool waitForEmpty = true)
        {
            Interlocked.Exchange(ref _stopped, 1);

            // If queue is already empty, signal drain complete
            if (Interlocked.CompareExchange(ref _pending, 0, 0) == 0)
            {
                _drainComplete.TrySetResult();
            }

            if (waitForEmpty)
            {
                _drainComplete.Task.GetAwaiter().GetResult();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _activitySignal.OnCompleted();
                    _activitySignal.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
