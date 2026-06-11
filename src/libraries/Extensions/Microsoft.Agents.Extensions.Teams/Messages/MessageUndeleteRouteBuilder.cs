// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.Proactive;

namespace Microsoft.Agents.Extensions.Teams.Messages;

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
    /// <param name="proactive">An instance of <see cref="Proactive"/> for handling proactive messaging.</param>
    /// <returns>The current <see cref="MessageUndeleteRouteBuilder"/> instance for method chaining.</returns>
    public MessageUndeleteRouteBuilder WithHandler(TeamsRouteHandler handler, Proactive proactive)
    {
        _route.Handler = HandlerUtils.WrapHandler(handler, proactive);
        return this;
    }
}
