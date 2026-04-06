// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System.Collections.Generic;

namespace Microsoft.Agents.Extensions.Teams;

/// <summary>
/// Provides extension methods on <see cref="Microsoft.Agents.Core.Models.IActivity"/> for interacting with Microsoft Teams-specific data.
/// </summary>
public static class TeamsActivityExtensions
{
    /// <summary>
    /// Gets the Team's selected channel id from the current activity.
    /// </summary>
    /// <param name="activity"> The current activity. </param>
    /// <returns>The current activity's team's selected channel, or empty string.</returns>
    public static string TeamsGetSelectedChannelId(this IActivity activity)
    {
        var channelData = activity.GetChannelData<Microsoft.Teams.Api.ChannelData>();
        return channelData?.Settings?.SelectedChannel?.Id;
    }

    /// <summary>
    /// Gets the Team's channel id from the current activity.
    /// </summary>
    /// <param name="activity"> The current activity. </param>
    /// <returns>The current activity's team's channel, or empty string.</returns>
    public static string TeamsGetChannelId(this IActivity activity)
    {
        var channelData = activity.GetChannelData<Microsoft.Teams.Api.ChannelData>();
        return channelData?.Channel?.Id;
    }

    /// <summary>
    /// Gets the TeamsMeetingInfo object from the current activity.
    /// </summary>
    /// <param name="activity">This activity.</param>
    /// <returns>The current activity's team's meeting, or null.</returns>
    public static Microsoft.Teams.Api.Meetings.Meeting TeamsGetMeetingInfo(this IActivity activity)
    {
        var channelData = activity.GetChannelData<Microsoft.Teams.Api.ChannelData>();
        if (channelData != null && channelData.Properties.TryGetValue("meeting", out var meetingObj))
        {
            return ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.Meetings.Meeting>(meetingObj);
        }

        return null;
    }

    /// <summary>
    /// Gets the TeamsInfo object from the current activity.
    /// </summary>
    /// <param name="activity">This activity.</param>
    /// <returns>The current activity's team information, or null.</returns>
    public static Microsoft.Teams.Api.Team TeamsGetTeamInfo(this IActivity activity)
    {
        var channelData = activity.GetChannelData<Microsoft.Teams.Api.ChannelData>();
        return channelData?.Team;
    }

    /// <summary>
    /// Configures the current activity to generate a notification within Teams.
    /// </summary>
    /// <param name="activity">The current activity. </param>
    /// <param name="alertInMeeting">Sent to a meeting chat, this will cause the Teams client to 
    /// render it in a notification popup as well as in the chat thread.</param>
    /// <param name="externalResourceUrl">Url to external resource. Must be included in manifest's valid domains.</param>
    public static void TeamsNotifyUser(this IActivity activity, bool alertInMeeting, string externalResourceUrl = null)
    {
        if (activity.ChannelData is not Microsoft.Teams.Api.ChannelData teamsChannelData)
        {
            teamsChannelData = new Microsoft.Teams.Api.ChannelData();
            activity.ChannelData = teamsChannelData;
        }

        teamsChannelData.Notification = new Microsoft.Teams.Api.Notification
        {
            Alert = !alertInMeeting,
            AlertInMeeting = alertInMeeting,
            ExternalResourceUrl = externalResourceUrl,
        };
    }

    /// <summary>
    /// Configures the current activity to generate a standard (non-meeting) notification within Teams.
    /// </summary>
    /// <param name="activity">The current activity.</param>
    public static void TeamsNotifyUser(this IActivity activity)
    {
        activity.TeamsNotifyUser(false);
    }

    /// <summary>
    /// Gets the Teams OnBehalfOf list from the current activity.
    /// </summary>
    /// <param name="activity">The current activity.</param>
    /// <returns>The current activity's OnBehalfOf list, or null.</returns>
    public static IList<Microsoft.Teams.Api.OnBehalfOf> TeamsGetTeamOnBehalfOf(this IActivity activity)
    {
        var channelData = activity.GetChannelData<Microsoft.Teams.Api.ChannelData>();
        return channelData?.OnBehalfOf;
    }

    /// <summary>
    /// Adds the Teams feedback loop flag to the current activity's ChannelData.
    /// The ChannelData must be null before calling this method; returns <c>false</c> if ChannelData is already set.
    /// </summary>
    /// <param name="activity">The current activity.</param>
    /// <param name="feedbackLoopType">The feedback loop type value. Defaults to <c>"default"</c>.</param>
    /// <returns><c>true</c> if the feedback loop flag was added; <c>false</c> if ChannelData was already populated.</returns>
    public static bool TeamsEnableFeedbackLoop(this IActivity activity, string feedbackLoopType = "default")
    {
        if (activity.ChannelData != null)
            return false;
        else
            activity.ChannelData = new
            {
                feedbackLoop = new
                {
                    type = feedbackLoopType
                }
            };
        return true;
    }
}
