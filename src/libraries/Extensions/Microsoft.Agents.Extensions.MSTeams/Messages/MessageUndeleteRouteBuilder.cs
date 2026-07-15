// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Extensions.MSTeams.App;
using System;

namespace Microsoft.Agents.Extensions.MSTeams.Messages;

/// <summary>
/// Provides a builder for configuring routes that handle Teams message undelete (undo soft-delete) events.
/// </summary>
/// <remarks>
/// Use <see cref="MessageUndeleteRouteBuilder"/> to create and configure routes that respond to Activity Type of
/// <see cref="Microsoft.Teams.Api.Activities.ActivityType.MessageUpdate"/> with <see cref="Microsoft.Teams.Api.ChannelData.EventType"/> of <c>"undeleteMessage"</c>.
/// </remarks>
public class MessageUndeleteRouteBuilder : MessageEventRouteBuilderBase<MessageUndeleteRouteBuilder>
{
    /// <summary>
    /// Creates a new instance of the MessageUndeleteRouteBuilder class for constructing route definitions.
    /// </summary>
    /// <returns>A MessageUndeleteRouteBuilder instance that can be used to configure and build routes.</returns>
    public static MessageUndeleteRouteBuilder Create()
    {
        var builder = Activator.CreateInstance<MessageUndeleteRouteBuilder>();
        return builder;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="MessageUndeleteRouteBuilder"/>,
    /// pre-configured to match Teams message undelete events.
    /// </summary>
    public MessageUndeleteRouteBuilder() : base()
    {
        ActivityTypeName = Microsoft.Teams.Api.Activities.ActivityType.MessageUpdate;
        EventTypeName = "undeleteMessage";
    }

    /// <summary>
    /// Configures the route to use the specified handler for processing message undelete events.
    /// </summary>
    /// <param name="handler">An asynchronous delegate that processes the message undelete event.</param>
    /// <returns>The current <see cref="MessageUndeleteRouteBuilder"/> instance for method chaining.</returns>
    public MessageUndeleteRouteBuilder WithHandler(TeamsRouteHandler handler)
    {
        AssertionHelpers.ThrowIfNull(handler, nameof(handler));
        _route.Handler = HandlerUtils.WrapHandler(handler);
        return this;
    }
}
