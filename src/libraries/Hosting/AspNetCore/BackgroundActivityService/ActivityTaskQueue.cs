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

        /// <summary>
        /// Enqueue an Activity, with Claims, to be processed on a background thread.
        /// </summary>
        /// <remarks>
        /// It is assumed these Claims have been authenticated via JwtTokenValidation.AuthenticateRequest 
        /// before enqueueing.
        /// </remarks>
        /// <param name="claimsIdentity">Authenticated <see cref="ClaimsIdentity"/> used to process the 
        /// activity.</param>
        /// <param name="activity"><see cref="Activity"/> to be processed.</param>
        public void QueueBackgroundActivity(ClaimsIdentity claimsIdentity, Activity activity)
        {
            ArgumentNullException.ThrowIfNull(claimsIdentity);
            ArgumentNullException.ThrowIfNull(activity);

            _activities.Enqueue(new ActivityWithClaims { ClaimsIdentity = claimsIdentity, Activity = activity});
            _signal.Release();
        }

        /// <summary>
        /// Wait for a signal of an enqueued Activity with Claims to be processed.
        /// </summary>
        /// <param name="cancellationToken">CancellationToken used to cancel the wait.</param>
        /// <returns>An ActivityWithClaims to be processed.
        /// </returns>
        /// <remarks>It is assumed these claims have already been authenticated via JwtTokenValidation.AuthenticateRequest.</remarks>
        public async Task<ActivityWithClaims> WaitForActivityAsync(CancellationToken cancellationToken)
        {
            await _signal.WaitAsync(cancellationToken);

            ActivityWithClaims dequeued;
            _activities.TryDequeue(out dequeued);

            return dequeued;
        }
    }
}
