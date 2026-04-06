// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Models;

namespace Microsoft.Agents.Extensions.Teams.Meetings;

/// <summary>
/// Provides a builder for configuring routes that handle Teams meeting participants join events.
/// </summary>
/// <remarks>
/// Use <see cref="MeetingParticipantsJoinRouteBuilder"/> to create and configure routes that respond to Activity Type of
/// <see cref="Microsoft.Agents.Core.Models.ActivityTypes.Event"/> with a name of
/// <see cref="Microsoft.Teams.Api.Activities.Events.Name.MeetingParticipantJoin"/>.
/// <code>
/// var route = MeetingParticipantsJoinRouteBuilder.Create()
///     .WithHandler(async (context, state, participants, ct) =>
///     {
///         // Handle participants joining
///     })
///     .Build();
///
/// app.AddRoute(route);
/// </code>
/// </remarks>
public class MeetingParticipantsJoinRouteBuilder : MeetingEventRouteBuilderBase<MeetingParticipantsJoinRouteBuilder>
{
    /// <summary>
    /// Initializes a new instance of <see cref="MeetingParticipantsJoinRouteBuilder"/>,
    /// pre-configured to match the Teams meeting participants join event.
    /// </summary>
    public MeetingParticipantsJoinRouteBuilder() : base()
    {
        EventName = Microsoft.Teams.Api.Activities.Events.Name.MeetingParticipantJoin;
    }

    /// <summary>
    /// Configures the route to use the specified handler for processing meeting participants join events.
    /// </summary>
    /// <param name="handler">An asynchronous delegate that processes the participants join event.
    /// Receives the turn context, turn state, deserialized <see cref="MeetingParticipantsEventDetails"/>,
    /// and a cancellation token.</param>
    /// <returns>The current <see cref="MeetingParticipantsJoinRouteBuilder"/> instance for method chaining.</returns>
    public MeetingParticipantsJoinRouteBuilder WithHandler(MeetingParticipantsEventHandler handler)
    {
        _route.Handler = (ctx, ts, ct) =>
        {
            var details = ProtocolJsonSerializer.ToObject<MeetingParticipantsEventDetails>(ctx.Activity.Value);
            return handler(ctx, ts, details, ct);
        };
        return this;
    }
}
