// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue
{
    /// <summary>
    /// Singleton queue, used to transfer an ActivityWithClaims to the <see cref="HostedActivityService"/>.
    /// </summary>
    public class ActivityTaskQueue : IActivityTaskQueue
    {
        private readonly SemaphoreSlim _signal = new(0);
        private readonly ConcurrentQueue<ActivityWithClaims> _activities = new ConcurrentQueue<ActivityWithClaims>();


        /// <inheritdoc/>
        public void QueueBackgroundActivity(ClaimsIdentity claimsIdentity, IActivity activity, bool proactive = false, string proactiveAudience = null, Type bot = null)
        {
            ArgumentNullException.ThrowIfNull(claimsIdentity);
            ArgumentNullException.ThrowIfNull(activity);

            _activities.Enqueue(new ActivityWithClaims { BotType = bot, ClaimsIdentity = claimsIdentity, Activity = activity, IsProactive = proactive, ProactiveAudience = proactiveAudience });
            _signal.Release();
        }

        /// <inheritdoc/>
        public async Task<ActivityWithClaims> WaitForActivityAsync(CancellationToken cancellationToken)
        {
            await _signal.WaitAsync(cancellationToken);

            _activities.TryDequeue(out ActivityWithClaims dequeued);

            return dequeued;
        }
    }
}
