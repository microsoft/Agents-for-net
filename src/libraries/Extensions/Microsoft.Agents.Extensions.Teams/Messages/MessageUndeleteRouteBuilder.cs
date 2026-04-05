// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Extensions.Teams.Messages;

/// <summary>
/// Provides a builder for configuring routes that handle Teams message undelete (undo soft-delete) events.
/// </summary>
/// <remarks>
/// Use <see cref="MessageUndeleteRouteBuilder"/> to create and configure routes that respond to
/// <c>messageUpdate</c> activities with channel-data event type <c>undeleteMessage</c>.
/// <code>
/// var route = MessageUndeleteRouteBuilder.Create()
///     .WithHandler(async (context, state, ct) =>
///     {
///         // Handle message undelete
///     })
///     .Build();
///
/// app.AddRoute(route);
/// </code>
/// </remarks>
public class MessageUndeleteRouteBuilder : MessageEventRouteBuilderBase<MessageUndeleteRouteBuilder>
{
    /// <summary>
    /// Initializes a new instance of <see cref="MessageUndeleteRouteBuilder"/>,
    /// pre-configured to match Teams message undelete events.
    /// </summary>
    public MessageUndeleteRouteBuilder() : base()
    {
        ActivityTypeName = ActivityTypes.MessageUpdate;
        EventTypeName = "undeleteMessage";
    }

    /// <summary>
    /// Configures the route to use the specified handler for processing message undelete events.
    /// </summary>
    /// <param name="handler">An asynchronous delegate that processes the message undelete event.</param>
    /// <returns>The current <see cref="MessageUndeleteRouteBuilder"/> instance for method chaining.</returns>
    public MessageUndeleteRouteBuilder WithHandler(RouteHandler handler)
    {
        _route.Handler = handler;
        return this;
    }
}
