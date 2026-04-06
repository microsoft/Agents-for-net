// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;

namespace Microsoft.Agents.Extensions.Teams.Messages;

/// <summary>
/// Provides a builder for configuring routes that handle Teams message edit events.
/// </summary>
/// <remarks>
/// Use <see cref="MessageEditRouteBuilder"/> to create and configure routes that respond to Activity Type of
/// <see cref="Microsoft.Teams.Api.Activities.ActivityType.MessageUpdate"/> with <see cref="Microsoft.Teams.Api.ChannelData.EventType"/> of <c>"editMessage"</c>.
/// </remarks>
public class MessageEditRouteBuilder : MessageEventRouteBuilderBase<MessageEditRouteBuilder>
{
    /// <summary>
    /// Initializes a new instance of <see cref="MessageEditRouteBuilder"/>,
    /// pre-configured to match Teams message edit events.
    /// </summary>
    public MessageEditRouteBuilder() : base()
    {
        ActivityTypeName = Microsoft.Teams.Api.Activities.ActivityType.MessageUpdate;
        EventTypeName = "editMessage";
    }

    /// <summary>
    /// Configures the route to use the specified handler for processing message edit events.
    /// </summary>
    /// <param name="handler">An asynchronous delegate that processes the message edit event.</param>
    /// <returns>The current <see cref="MessageEditRouteBuilder"/> instance for method chaining.</returns>
    public MessageEditRouteBuilder WithHandler(RouteHandler handler)
    {
        _route.Handler = handler;
        return this;
    }
}
