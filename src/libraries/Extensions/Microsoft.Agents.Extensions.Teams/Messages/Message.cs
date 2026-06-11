// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Extensions.Teams.Messages;

/// <summary>
/// Provides methods for registering handlers to process message-related events, such as edits, deletions, undeletions,
/// and read receipts, within a specific channel context.
/// </summary>
public class Message
{
    private readonly AgentApplication _app;
    private readonly ChannelId _channelId;

    internal Message(AgentApplication app, ChannelId channelId)
    {
        _app = app;
        _channelId = channelId;
    }

    /// <summary>
    /// Handles message edit events.
    /// </summary>
    /// <remarks>
    /// Alternatively, the <see cref="MessageEditRouteAttribute"/> can be used to decorate a <see cref="RouteHandler"/> method for the same purpose.
    /// </remarks>
    /// <param name="handler">Function to call when the event is triggered.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <returns>The AgentExtension instance for chaining purposes.</returns>
    public Message OnMessageEdit(RouteHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(MessageEditRouteBuilder.Create()
            .WithChannelId(_channelId).WithOrderRank(rank)
            .WithHandler(handler)
            .WithOAuthHandlers(autoSignInHandlers)
            .Build());
        return this;
    }

    /// <summary>
    /// Handles message undelete (undo soft-delete) events.
    /// </summary>
    /// <remarks>
    /// Alternatively, the <see cref="MessageUndeleteRouteAttribute"/> can be used to decorate a <see cref="RouteHandler"/> method for the same purpose.
    /// </remarks>
    /// <param name="handler">Function to call when the event is triggered.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <returns>The AgentExtension instance for chaining purposes.</returns>
    public Message OnMessageUndelete(RouteHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(MessageUndeleteRouteBuilder.Create()
            .WithChannelId(_channelId).WithOrderRank(rank)
            .WithHandler(handler)
            .WithOAuthHandlers(autoSignInHandlers)
            .Build());
        return this;
    }

    /// <summary>
    /// Handles message soft-delete events.
    /// </summary>
    /// <remarks>
    /// Alternatively, the <see cref="MessageDeleteRouteAttribute"/> can be used to decorate a <see cref="RouteHandler"/> method for the same purpose.
    /// </remarks>
    /// <param name="handler">Function to call when the event is triggered.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <returns>The AgentExtension instance for chaining purposes.</returns>
    public Message OnMessageDelete(RouteHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(MessageDeleteRouteBuilder.Create()
            .WithChannelId(_channelId).WithOrderRank(rank)
            .WithHandler(handler)
            .WithOAuthHandlers(autoSignInHandlers)
            .Build());
        return this;
    }

    /// <summary>
    /// Handles read receipt events for messages sent by the agent in personal scope.
    /// </summary>
    /// <remarks>
    /// Alternatively, the <see cref="ReadReceiptRouteAttribute"/> can be used to decorate a <see cref="ReadReceiptHandler"/> method for the same purpose.
    /// </remarks>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <returns>The AgentExtension instance for chaining purposes.</returns>
    public Message OnReadReceipt(ReadReceiptHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(ReadReceiptRouteBuilder.Create()
            .WithChannelId(_channelId).WithOrderRank(rank)
            .WithHandler(handler)
            .WithOAuthHandlers(autoSignInHandlers)
            .Build());
        return this;
    }

    /// <summary>
    /// Handles O365 Connector Card Action activities.
    /// </summary>
    /// <remarks>
    /// Alternatively, the <see cref="O365ConnectorCardActionRouteAttribute"/> can be used to decorate an <see cref="O365ConnectorCardActionHandler"/> method for the same purpose.
    /// </remarks>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <returns>The AgentExtension instance for chaining purposes.</returns>
    public Message OnO365ConnectorCardAction(O365ConnectorCardActionHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(O365ConnectorCardActionRouteBuilder.Create()
            .WithChannelId(_channelId).WithOrderRank(rank)
            .WithHandler(handler)
            .WithOAuthHandlers(autoSignInHandlers)
            .Build());
        return this;
    }
}
