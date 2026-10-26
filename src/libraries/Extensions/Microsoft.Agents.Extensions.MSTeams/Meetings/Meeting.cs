// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Extensions.MSTeams.Meetings;

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
    /// <remarks>Alternatively, the <see cref="Microsoft.Agents.Extensions.MSTeams.Meetings.TeamsMeetingStartRouteAttribute"/> can be used to decorate a <see cref="Microsoft.Agents.Extensions.MSTeams.Meetings.MeetingStartHandler"/> method for the same purpose.</remarks>
    /// <param name="handler">Function to call when a Microsoft Teams meeting start event activity is received from the connector.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="Microsoft.Agents.Builder.App.RouteRank.Unspecified"/>.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public Meeting OnStart(MeetingStartHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(MeetingStartRouteBuilder.Create().WithChannelId(_channelId).WithOrderRank(rank).WithHandler(handler).WithOAuthHandlers(autoSignInHandlers).Build());
        return this;
    }

    /// <summary>
    /// Handles Microsoft Teams meeting end events.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="Microsoft.Agents.Extensions.MSTeams.Meetings.TeamsMeetingEndRouteAttribute"/> can be used to decorate a <see cref="Microsoft.Agents.Extensions.MSTeams.Meetings.MeetingEndHandler"/> method for the same purpose.</remarks>
    /// <param name="handler">Function to call when a Microsoft Teams meeting end event activity is received from the connector.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="Microsoft.Agents.Builder.App.RouteRank.Unspecified"/>.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public Meeting OnEnd(MeetingEndHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(MeetingEndRouteBuilder.Create().WithChannelId(_channelId).WithOrderRank(rank).WithHandler(handler).WithOAuthHandlers(autoSignInHandlers).Build());
        return this;
    }

    /// <summary>
    /// Handles Microsoft Teams meeting participants join events.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="Microsoft.Agents.Extensions.MSTeams.Meetings.TeamsMeetingParticipantsJoinRouteAttribute"/> can be used to decorate a <see cref="Microsoft.Agents.Extensions.MSTeams.Meetings.MeetingParticipantsJoinHandler"/> method for the same purpose.</remarks>
    /// <param name="handler">Function to call when a Microsoft Teams meeting participants join event activity is received from the connector.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="Microsoft.Agents.Builder.App.RouteRank.Unspecified"/>.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public Meeting OnParticipantsJoin(MeetingParticipantsJoinHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(MeetingParticipantsJoinRouteBuilder.Create().WithChannelId(_channelId).WithOrderRank(rank).WithHandler(handler).WithOAuthHandlers(autoSignInHandlers).Build());
        return this;
    }

    /// <summary>
    /// Handles Microsoft Teams meeting participants leave events.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="Microsoft.Agents.Extensions.MSTeams.Meetings.TeamsMeetingParticipantsLeaveRouteAttribute"/> can be used to decorate a <see cref="Microsoft.Agents.Extensions.MSTeams.Meetings.MeetingParticipantsLeaveHandler"/> method for the same purpose.</remarks>
    /// <param name="handler">Function to call when a Microsoft Teams meeting participants leave event activity is received from the connector.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="Microsoft.Agents.Builder.App.RouteRank.Unspecified"/>.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public Meeting OnParticipantsLeave(MeetingParticipantsLeaveHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(MeetingParticipantsLeaveRouteBuilder.Create().WithChannelId(_channelId).WithOrderRank(rank).WithHandler(handler).WithOAuthHandlers(autoSignInHandlers).Build());
        return this;
    }
}
