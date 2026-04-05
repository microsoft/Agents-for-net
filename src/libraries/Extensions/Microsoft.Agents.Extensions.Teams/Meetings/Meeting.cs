// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Extensions.Teams.Meetings;

/// <summary>
/// Meetings class to enable fluent style registration of handlers related to Microsoft Teams Meetings.
/// </summary>
public class Meeting
{
    private readonly AgentApplication _app;
    private readonly ChannelId _channelId;

    internal Meeting(AgentApplication app, ChannelId channelId)
    {
        _app = app;
        _channelId = channelId;
    }

    /// <summary>
    /// Handles Microsoft Teams meeting start events.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="MeetingStartRouteAttribute"/> can be used to decorate a <see cref="MeetingStartHandler"/> method for the same purpose.</remarks>
    /// <param name="handler">Function to call when a Microsoft Teams meeting start event activity is received from the connector.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public Meeting OnStart(MeetingStartHandler handler)
    {
        _app.AddRoute(MeetingStartRouteBuilder.Create().WithChannelId(_channelId).WithHandler(handler).Build());
        return this;
    }

    /// <summary>
    /// Handles Microsoft Teams meeting end events.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="MeetingEndRouteAttribute"/> can be used to decorate a <see cref="MeetingEndHandler"/> method for the same purpose.</remarks>
    /// <param name="handler">Function to call when a Microsoft Teams meeting end event activity is received from the connector.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public Meeting OnEnd(MeetingEndHandler handler)
    {
        _app.AddRoute(MeetingEndRouteBuilder.Create().WithChannelId(_channelId).WithHandler(handler).Build());
        return this;
    }

    /// <summary>
    /// Handles Microsoft Teams meeting participants join events.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="MeetingParticipantsJoinRouteAttribute"/> can be used to decorate a <see cref="MeetingParticipantsEventHandler"/> method for the same purpose.</remarks>
    /// <param name="handler">Function to call when a Microsoft Teams meeting participants join event activity is received from the connector.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public Meeting OnParticipantsJoin(MeetingParticipantsEventHandler handler)
    {
        _app.AddRoute(MeetingParticipantsJoinRouteBuilder.Create().WithChannelId(_channelId).WithHandler(handler).Build());
        return this;
    }

    /// <summary>
    /// Handles Microsoft Teams meeting participants leave events.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="MeetingParticipantsLeaveRouteAttribute"/> can be used to decorate a <see cref="MeetingParticipantsEventHandler"/> method for the same purpose.</remarks>
    /// <param name="handler">Function to call when a Microsoft Teams meeting participants leave event activity is received from the connector.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public Meeting OnParticipantsLeave(MeetingParticipantsEventHandler handler)
    {
        _app.AddRoute(MeetingParticipantsLeaveRouteBuilder.Create().WithChannelId(_channelId).WithHandler(handler).Build());
        return this;
    }
}
