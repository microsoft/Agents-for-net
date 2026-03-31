// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Extensions.Teams.App.Meetings;

/// <summary>
/// Provides a builder for configuring routes that handle Teams meeting end events.
/// </summary>
/// <remarks>
/// Use <see cref="MeetingEndRouteBuilder"/> to create and configure routes that respond to
/// <c>application/vnd.microsoft.meetingEnd</c> event activities.
/// <code>
/// var route = MeetingEndRouteBuilder.Create()
///     .WithHandler(async (context, state, meeting, ct) =>
///     {
///         // Handle meeting end
///     })
///     .Build();
///
/// app.AddRoute(route);
/// </code>
/// </remarks>
public class MeetingEndRouteBuilder : MeetingEventRouteBuilderBase<MeetingEndRouteBuilder>
{
    /// <summary>
    /// Initializes a new instance of <see cref="MeetingEndRouteBuilder"/>,
    /// pre-configured to match the Teams meeting end event.
    /// </summary>
    public MeetingEndRouteBuilder() : base()
    {
        EventName = Microsoft.Teams.Api.Activities.Events.Name.MeetingEnd;
    }

    /// <summary>
    /// Configures the route to use the specified handler for processing meeting end events.
    /// </summary>
    /// <param name="handler">An asynchronous delegate that processes the meeting end event.
    /// Receives the turn context, turn state, deserialized <see cref="Microsoft.Teams.Api.Meetings.MeetingDetails"/>,
    /// and a cancellation token.</param>
    /// <returns>The current <see cref="MeetingEndRouteBuilder"/> instance for method chaining.</returns>
    public MeetingEndRouteBuilder WithHandler(MeetingEndHandler handler)
    {
        _route.Handler = (ctx, ts, ct) =>
        {
            var details = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.Meetings.MeetingDetails>(ctx.Activity.Value);
            return handler(ctx, ts, details, ct);
        };
        return this;
    }
}
