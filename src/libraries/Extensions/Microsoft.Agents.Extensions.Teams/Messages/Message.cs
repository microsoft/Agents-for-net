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
    /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
    /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
    /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
    /// <returns>The AgentExtension instance for chaining purposes.</returns>
    public Message OnMessageEdit(RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
    {
        _app.AddRoute(MessageEditRouteBuilder.Create()
            .WithChannelId(_channelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
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
    /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
    /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
    /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
    public Message OnMessageUndelete(RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
    {
        _app.AddRoute(MessageUndeleteRouteBuilder.Create()
            .WithChannelId(_channelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
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
    /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
    /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
    /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
    public Message OnMessageDelete(RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
    {
        _app.AddRoute(MessageDeleteRouteBuilder.Create()
            .WithChannelId(_channelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
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
    /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
    /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
    /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
    public Message OnReadReceipt(ReadReceiptHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
    {
        _app.AddRoute(ReadReceiptRouteBuilder.Create()
            .WithChannelId(_channelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
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
    /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
    /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
    /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
    /// <returns>The AgentExtension instance for chaining purposes.</returns>
    public Message OnO365ConnectorCardAction(O365ConnectorCardActionHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
    {
        _app.AddRoute(O365ConnectorCardActionRouteBuilder.Create()
            .WithChannelId(_channelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
            .WithHandler(handler)
            .WithOAuthHandlers(autoSignInHandlers)
            .Build());
        return this;
    }
}
