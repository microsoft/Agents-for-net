// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using System;

namespace Microsoft.Agents.Extensions.MSTeams.Meetings;

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
    /// Creates a new instance of the MeetingStartRouteBuilder class for constructing route definitions.
    /// </summary>
    /// <returns>A MeetingStartRouteBuilder instance that can be used to configure and build routes.</returns>
    public static MeetingStartRouteBuilder Create()
    {
        var builder = Activator.CreateInstance<MeetingStartRouteBuilder>();
        return builder;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="MeetingStartRouteBuilder"/>,
    /// pre-configured to match the Teams meeting start event.
    /// </summary>
    public MeetingStartRouteBuilder() : base()
    {
        EventName = Microsoft.Teams.Api.Activities.Events.Name.MeetingStart;
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
            var details = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.Meetings.MeetingDetails>(ctx.Activity.Value);
            return handler(new TeamsTurnContext(ctx), ts, details, ct);
        };
        return this;
    }
}
