// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Models;

namespace Microsoft.Agents.Extensions.Teams.App.Meetings;

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
    /// <param name="handler">Function to call when a Microsoft Teams meeting start event activity is received from the connector.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public Meeting OnStart(MeetingStartHandler handler)
    {
        _app.AddRoute(EventRouteBuilder.Create().WithChannelId(_channelId).WithName(Microsoft.Teams.Api.Activities.Events.Name.MeetingStart)
            .WithHandler((ctx, ts, ct) =>
            {
                var meeting = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.Meetings.MeetingDetails>(ctx.Activity.Value);
                return handler(ctx, ts, meeting, ct);
            })
            .Build());
        return this;
    }

    /// <summary>
    /// Handles Microsoft Teams meeting end events.
    /// </summary>
    /// <param name="handler">Function to call when a Microsoft Teams meeting end event activity is received from the connector.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public Meeting OnEnd(MeetingEndHandler handler)
    {
        _app.AddRoute(EventRouteBuilder.Create().WithChannelId(_channelId).WithName(Microsoft.Teams.Api.Activities.Events.Name.MeetingEnd)
            .WithHandler((ctx, ts, ct) =>
            {
                var meeting = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.Meetings.MeetingDetails>(ctx.Activity.Value);
                return handler(ctx, ts, meeting, ct);
            })
            .Build());
        return this;
    }

    /// <summary>
    /// Handles Microsoft Teams meeting participants join events.
    /// </summary>
    /// <param name="handler">Function to call when a Microsoft Teams meeting participants join event activity is received from the connector.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public Meeting OnParticipantsJoin(MeetingParticipantsEventHandler handler)
    {
        _app.AddRoute(EventRouteBuilder.Create().WithChannelId(_channelId).WithName(Microsoft.Teams.Api.Activities.Events.Name.MeetingParticipantJoin)
            .WithHandler((ctx, ts, ct) =>
            {
                var eventDetails = ProtocolJsonSerializer.ToObject<MeetingParticipantsEventDetails>(ctx.Activity.Value);
                return handler(ctx, ts, eventDetails, ct);
            })
            .Build());
        return this;
    }

    /// <summary>
    /// Handles Microsoft Teams meeting participants leave events.
    /// </summary>
    /// <param name="handler">Function to call when a Microsoft Teams meeting participants leave event activity is received from the connector.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public Meeting OnParticipantsLeave(MeetingParticipantsEventHandler handler)
    {
        _app.AddRoute(EventRouteBuilder.Create().WithChannelId(_channelId).WithName(Microsoft.Teams.Api.Activities.Events.Name.MeetingParticipantLeave)
            .WithHandler((ctx, ts, ct) =>
            {
                var eventDetails = ProtocolJsonSerializer.ToObject<MeetingParticipantsEventDetails>(ctx.Activity.Value);
                return handler(ctx, ts, eventDetails, ct);
            })
            .Build());
        return this;
    }
}
