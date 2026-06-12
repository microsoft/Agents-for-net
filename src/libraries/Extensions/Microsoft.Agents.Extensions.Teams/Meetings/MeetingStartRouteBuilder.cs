// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Extensions.Teams.Meetings;

/// <summary>
/// Provides a builder for configuring routes that handle Teams meeting start events.
/// </summary>
/// <remarks>
/// Use <see cref="MeetingStartRouteBuilder"/> to create and configure routes that respond to Activity Type of
/// <see cref="Microsoft.Agents.Core.Models.ActivityTypes.Event"/> with a name of
/// <see cref="Microsoft.Teams.Api.Activities.Events.Name.MeetingStart"/>.
/// </remarks>
public class MeetingStartRouteBuilder : MeetingEventRouteBuilderBase<MeetingStartRouteBuilder>
{
    /// <summary>
    /// Initializes a new instance of <see cref="MeetingStartRouteBuilder"/>,
    /// pre-configured to match the Teams meeting start event.
    /// </summary>
    public MeetingStartRouteBuilder() : base()
    {
        EventName = Microsoft.Teams.Api.Activities.Events.Name.MeetingStart;
    }

    /// <summary>
    /// Creates a new instance of the <see cref="MeetingStartRouteBuilder"/> class.
    /// </summary>
    /// <returns>A new <see cref="MeetingStartRouteBuilder"/>.</returns>
    public static MeetingStartRouteBuilder Create()
    {
        return new MeetingStartRouteBuilder();
    }

    /// <summary>
    /// Configures the route to use the specified handler for processing meeting start events.
    /// </summary>
    /// <param name="handler">An asynchronous delegate that processes the meeting start event.
    /// Receives the turn context, turn state, deserialized <see cref="Microsoft.Teams.Api.Meetings.MeetingDetails"/>,
    /// and a cancellation token.</param>
    /// <returns>The current <see cref="MeetingStartRouteBuilder"/> instance for method chaining.</returns>
    public MeetingStartRouteBuilder WithHandler(MeetingStartHandler handler)
    {
        _route.Handler = (ctx, ts, ct) =>
        {
            var ttc = new TeamsTurnContext(ctx);
            var details = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.Meetings.MeetingDetails>(ttc.Activity.Value);
            return handler(ttc, ts, details, ct);
        };
        return this;
    }
}
