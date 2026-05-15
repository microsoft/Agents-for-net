// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams;

/// <summary>
/// Provides extension methods on <see cref="ITurnContext"/> for sending targeted activities in Microsoft Teams conversations.
/// </summary>
public static class TeamsTurnContextExtensions
{
    /// <summary>
    /// Sends an activity to the conversation with a targeted treatment, allowing the activity to be directed to a
    /// specific recipient or group within the conversation.
    /// </summary>
    /// <remarks>This extension method adds a targeted treatment to the activity before sending it.
    /// Use this method when you need to direct an activity to a specific recipient or subset of participants in a
    /// conversation. The activity's Entities collection will be updated to include the targeted
    /// treatment.</remarks>
    /// <param name="turnContext">The context for the current conversation turn.</param>
    /// <param name="activity">The activity to send. Must represent the message or event to be delivered and cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the send operation.</param>
    /// <returns>A task that represents the asynchronous send operation. The task result contains a ResourceResponse with
    /// information about the sent activity.</returns>
    public static Task<ResourceResponse> SendTargetedActivityAsync(this ITurnContext turnContext, IActivity activity, CancellationToken cancellationToken = default)
    {
        return turnContext.SendActivityAsync(activity.Clone().MakeTargetedActivity(), cancellationToken);
    }

    /// <summary>
    /// Sends a set of activities to targeted recipients within the current turn context asynchronously.
    /// </summary>
    /// <remarks>Each activity is cloned and marked as targeted before being sent. Use this method
    /// when you need to deliver activities to specific recipients rather than broadcasting to all
    /// participants.</remarks>
    /// <param name="turnContext">The context for the current conversation turn.</param>
    /// <param name="activities">An array of activities to send. Each activity will be treated as targeted. Cannot be null and must not
    /// contain null elements.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the send operation.</param>
    /// <returns>A task that represents the asynchronous send operation. The task result contains an array of
    /// ResourceResponse objects for each sent activity.</returns>
    public static Task<ResourceResponse[]> SendTargetedActivitiesAsync(this ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken = default)
    {
        var clonedActivities = new List<IActivity>(activities.Length);
        foreach (var activity in activities)
        {
            clonedActivities.Add(activity.Clone().MakeTargetedActivity());
        }

        return turnContext.SendActivitiesAsync([.. clonedActivities], cancellationToken);
    }
}
