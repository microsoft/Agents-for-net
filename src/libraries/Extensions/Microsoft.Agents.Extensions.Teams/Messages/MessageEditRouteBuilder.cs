// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Extensions.Teams.Messages;

/// <summary>
/// Provides a builder for configuring routes that handle Teams message edit events.
/// </summary>
/// <remarks>
/// Use <see cref="MessageEditRouteBuilder"/> to create and configure routes that respond to
/// <c>messageUpdate</c> activities with channel-data event type <c>editMessage</c>.
/// <code>
/// var route = MessageEditRouteBuilder.Create()
///     .WithHandler(async (context, state, ct) =>
///     {
///         // Handle message edit
///     })
///     .Build();
///
/// app.AddRoute(route);
/// </code>
/// </remarks>
public class MessageEditRouteBuilder : MessageEventRouteBuilderBase<MessageEditRouteBuilder>
{
    /// <summary>
    /// Initializes a new instance of <see cref="MessageEditRouteBuilder"/>,
    /// pre-configured to match Teams message edit events.
    /// </summary>
    public MessageEditRouteBuilder() : base()
    {
        ActivityTypeName = ActivityTypes.MessageUpdate;
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
