// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.MSTeams.App;

namespace Microsoft.Agents.Extensions.MSTeams.Messages;

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
    /// Alternatively, the <see cref="TeamsMessageEditRouteAttribute"/> can be used to decorate a <see cref="RouteHandler"/> method for the same purpose.
    /// </remarks>
    /// <param name="handler">Function to call when the event is triggered.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <returns>The AgentExtension instance for chaining purposes.</returns>
    public Message OnMessageEdit(TeamsRouteHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
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
    /// Alternatively, the <see cref="TeamsMessageUndeleteRouteAttribute"/> can be used to decorate a <see cref="RouteHandler"/> method for the same purpose.
    /// </remarks>
    /// <param name="handler">Function to call when the event is triggered.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <returns>The AgentExtension instance for chaining purposes.</returns>
    public Message OnMessageUndelete(TeamsRouteHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
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
    /// Alternatively, the <see cref="TeamsMessageDeleteRouteAttribute"/> can be used to decorate a <see cref="RouteHandler"/> method for the same purpose.
    /// </remarks>
    /// <param name="handler">Function to call when the event is triggered.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <returns>The AgentExtension instance for chaining purposes.</returns>
    public Message OnMessageDelete(TeamsRouteHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
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
    /// Alternatively, the <see cref="TeamsReadReceiptRouteAttribute"/> can be used to decorate a <see cref="ReadReceiptHandler"/> method for the same purpose.
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
    /// Alternatively, the <see cref="TeamsExecuteActionRouteAttribute"/> can be used to decorate an <see cref="O365ConnectorCardActionHandler"/> method for the same purpose.
    /// </remarks>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <returns>The AgentExtension instance for chaining purposes.</returns>
    public Message OnExecuteAction(ExecuteActionRouteHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(ExecuteActionRouteBuilder.Create()
            .WithChannelId(_channelId).WithOrderRank(rank)
            .WithHandler(handler)
            .WithOAuthHandlers(autoSignInHandlers)
            .Build());
        return this;
    }
}
