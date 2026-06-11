// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Models;

namespace Microsoft.Agents.Extensions.Teams.Meetings;

/// <summary>
/// Provides a builder for configuring routes that handle Teams meeting participants leave events.
/// </summary>
/// <remarks>
/// Use <see cref="MeetingParticipantsLeaveRouteBuilder"/> to create and configure routes that respond to Activity Type of
/// <see cref="Microsoft.Agents.Core.Models.ActivityTypes.Event"/> with a name of
/// <see cref="Microsoft.Teams.Api.Activities.Events.Name.MeetingParticipantLeave"/>.
/// </remarks>
public class MeetingParticipantsLeaveRouteBuilder : MeetingEventRouteBuilderBase<MeetingParticipantsLeaveRouteBuilder>
{
    /// <summary>
    /// Initializes a new instance of <see cref="MeetingParticipantsLeaveRouteBuilder"/>,
    /// pre-configured to match the Teams meeting participants leave event.
    /// </summary>
    public MeetingParticipantsLeaveRouteBuilder() : base()
    {
        EventName = Microsoft.Teams.Api.Activities.Events.Name.MeetingParticipantLeave;
    }

    /// <summary>
    /// Configures the route to use the specified handler for processing meeting participants leave events.
    /// </summary>
    /// <param name="handler">An asynchronous delegate that processes the participants leave event.
    /// Receives the turn context, turn state, deserialized <see cref="MeetingParticipantsEventDetails"/>,
    /// and a cancellation token.</param>
    /// <returns>The current <see cref="MeetingParticipantsLeaveRouteBuilder"/> instance for method chaining.</returns>
    public MeetingParticipantsLeaveRouteBuilder WithHandler(MeetingParticipantsEventHandler handler)
    {
        _route.Handler = (ctx, ts, ct) =>
        {
            var details = ProtocolJsonSerializer.ToObject<MeetingParticipantsEventDetails>(ctx.Activity.Value);
            return handler(ctx, ts, details, ct);
        };
        return this;
    }
}
