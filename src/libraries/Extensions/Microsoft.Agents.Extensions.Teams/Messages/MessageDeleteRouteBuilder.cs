// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Extensions.Teams.Messages;

/// <summary>
/// Provides a builder for configuring routes that handle Teams message soft-delete events.
/// </summary>
/// <remarks>
/// Use <see cref="MessageDeleteRouteBuilder"/> to create and configure routes that respond to
/// <c>messageDelete</c> activities with channel-data event type <c>softDeleteMessage</c>.
/// <code>
/// var route = MessageDeleteRouteBuilder.Create()
///     .WithHandler(async (context, state, ct) =>
///     {
///         // Handle message delete
///     })
///     .Build();
///
/// app.AddRoute(route);
/// </code>
/// </remarks>
public class MessageDeleteRouteBuilder : MessageEventRouteBuilderBase<MessageDeleteRouteBuilder>
{
    /// <summary>
    /// Initializes a new instance of <see cref="MessageDeleteRouteBuilder"/>,
    /// pre-configured to match Teams message soft-delete events.
    /// </summary>
    public MessageDeleteRouteBuilder() : base()
    {
        ActivityTypeName = ActivityTypes.MessageDelete;
        EventTypeName = "softDeleteMessage";
    }

    /// <summary>
    /// Configures the route to use the specified handler for processing message soft-delete events.
    /// </summary>
    /// <param name="handler">An asynchronous delegate that processes the message soft-delete event.</param>
    /// <returns>The current <see cref="MessageDeleteRouteBuilder"/> instance for method chaining.</returns>
    public MessageDeleteRouteBuilder WithHandler(RouteHandler handler)
    {
        _route.Handler = handler;
        return this;
    }
}
