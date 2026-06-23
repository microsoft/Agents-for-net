// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.MSTeams
{
    /// <summary>
    /// Provides Teams-specific helpers for working with the current <see cref="ITurnContext"/>.
    /// </summary>
    public interface ITeamsTurnContext : ITurnContext
    {
        /// <summary>
        /// Sends an activity to the conversation with a targeted treatment, allowing the activity to be directed to a
        /// specific recipient or group within the conversation.
        /// </summary>
        /// <param name="activity">The activity to send. Must represent the message or event to be delivered and cannot be null.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the send operation.</param>
        /// <returns>A task that represents the asynchronous send operation. The task result contains a ResourceResponse with
        /// information about the sent activity.</returns>
        Task<ResourceResponse> SendTargetedActivityAsync(IActivity activity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a set of activities to targeted recipients within the current turn context asynchronously.
        /// </summary>
        /// <param name="activities">An array of activities to send. Each activity will be treated as targeted. Cannot be null and must not
        /// contain null elements.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the send operation.</param>
        /// <returns>A task that represents the asynchronous send operation. The task result contains an array of
        /// ResourceResponse objects for each sent activity.</returns>
        Task<ResourceResponse[]> SendTargetedActivitiesAsync(IActivity[] activities, CancellationToken cancellationToken = default);
    }
}
