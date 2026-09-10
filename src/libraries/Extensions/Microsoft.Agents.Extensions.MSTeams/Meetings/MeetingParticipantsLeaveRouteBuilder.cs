// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using Microsoft.Teams.Apps.Meetings;
using System;

namespace Microsoft.Agents.Extensions.MSTeams.Meetings;

/// <summary>
/// Provides a builder for configuring routes that handle Teams meeting participants leave events.
/// </summary>
/// <remarks>
/// Use <see cref="MeetingParticipantsLeaveRouteBuilder"/> to create and configure routes that respond to Activity Type of
/// <see cref="Microsoft.Agents.Core.Models.ActivityTypes.Event"/> with a name of
/// <see cref="Microsoft.Teams.Apps.EventNames.MeetingParticipantLeave"/>.
/// </remarks>
public class MeetingParticipantsLeaveRouteBuilder : MeetingEventRouteBuilderBase<MeetingParticipantsLeaveRouteBuilder>
{
    /// <summary>
    /// Creates a new instance of the MeetingParticipantsLeaveRouteBuilder class for constructing route definitions.
    /// </summary>
    /// <returns>A MeetingParticipantsLeaveRouteBuilder instance that can be used to configure and build routes.</returns>
    public static MeetingParticipantsLeaveRouteBuilder Create()
    {
        var builder = Activator.CreateInstance<MeetingParticipantsLeaveRouteBuilder>();
        return builder;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="MeetingParticipantsLeaveRouteBuilder"/>,
    /// pre-configured to match the Teams meeting participants leave event.
    /// </summary>
    public MeetingParticipantsLeaveRouteBuilder() : base()
    {
        EventName = Microsoft.Teams.Apps.EventNames.MeetingParticipantLeave;
    }

    /// <summary>
    /// Configures the route to use the specified handler for processing meeting participants leave events.
    /// </summary>
    /// <param name="handler">An asynchronous delegate that processes the participants leave event.
    /// Receives the turn context, turn state, deserialized <see cref="MeetingParticipantLeaveValue"/>,
    /// and a cancellation token.</param>
    /// <returns>The current <see cref="MeetingParticipantsLeaveRouteBuilder"/> instance for method chaining.</returns>
    public MeetingParticipantsLeaveRouteBuilder WithHandler(MeetingParticipantsLeaveHandler handler)
    {
        _route.Handler = (ctx, ts, ct) =>
        {
            var details = ProtocolJsonSerializer.ToObject<MeetingParticipantLeaveValue>(ctx.Activity.Value);
            return handler(new TeamsTurnContext(ctx), ts, details, ct);
        };
        return this;
    }
}
