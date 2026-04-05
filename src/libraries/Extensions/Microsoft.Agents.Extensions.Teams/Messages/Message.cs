// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

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
    /// <param name="handler">Function to call when the event is triggered.</param>
    /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
    /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
    /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
    /// <returns>The AgentExtension instance for chaining purposes.</returns>
    public Message OnMessageEdit(RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
    {
        _app.AddRoute(TypeRouteBuilder.Create()
            .WithType(ActivityTypes.MessageUpdate)
            .WithChannelId(_channelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
            .WithSelector((turnContext, cancellationToken) =>
            {
                Microsoft.Teams.Api.ChannelData ChannelData = turnContext.Activity.GetChannelData<Microsoft.Teams.Api.ChannelData>();
                return Task.FromResult(string.Equals(ChannelData?.EventType, "editMessage", StringComparison.OrdinalIgnoreCase));
            })
            .WithHandler(handler)
            .WithOAuthHandlers(autoSignInHandlers)
            .Build());
        return this;
    }

    /// <summary>
    /// Handles message undo soft delete events.
    /// </summary>
    /// <param name="handler">Function to call when the event is triggered.</param>
    /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
    /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
    /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
    public Message OnMessageUndelete(RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
    {
        _app.AddRoute(TypeRouteBuilder.Create()
            .WithType(ActivityTypes.MessageUpdate)
            .WithChannelId(_channelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
            .WithSelector((turnContext, cancellationToken) =>
            {
                Microsoft.Teams.Api.ChannelData ChannelData = turnContext.Activity.GetChannelData<Microsoft.Teams.Api.ChannelData>();
                return Task.FromResult(string.Equals(ChannelData?.EventType, "undeleteMessage", StringComparison.OrdinalIgnoreCase));
            })
            .WithHandler(handler)
            .WithOAuthHandlers(autoSignInHandlers)
            .Build());
        return this;
    }

    /// <summary>
    /// Handles message soft delete events.
    /// </summary>
    /// <param name="handler">Function to call when the event is triggered.</param>
    /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
    /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
    /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
    public Message OnMessageDelete(RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
    {
        _app.AddRoute(TypeRouteBuilder.Create()
            .WithType(ActivityTypes.MessageDelete)
            .WithChannelId(_channelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
            .WithSelector((turnContext, cancellationToken) =>
            {
                Microsoft.Teams.Api.ChannelData channelData = turnContext.Activity.GetChannelData<Microsoft.Teams.Api.ChannelData>();
                return Task.FromResult(string.Equals(channelData?.EventType, "softDeleteMessage", StringComparison.OrdinalIgnoreCase));
            })
            .WithHandler(handler)
            .WithOAuthHandlers(autoSignInHandlers)
            .Build());
        return this;
    }

    /// <summary>
    /// Handles read receipt events for messages sent by the agent in personal scope.
    /// </summary>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
    /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
    /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
    public Message OnReadReceipt(ReadReceiptHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
    {
        _app.AddRoute(EventRouteBuilder.Create()
            .WithName(Microsoft.Teams.Api.Activities.Events.Name.ReadReceipt)
            .WithChannelId(_channelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
            .WithHandler(async (ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken) =>
            {
                JsonElement readReceiptInfo = (JsonElement)turnContext.Activity.Value;
                await handler(turnContext, turnState, readReceiptInfo, cancellationToken).ConfigureAwait(false);
            })
            .WithOAuthHandlers(autoSignInHandlers)
            .Build());
        return this;
    }

    /// <summary>
    /// Handles O365 Connector Card Action activities.
    /// </summary>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
    /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
    /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
    /// <returns>The AgentExtension instance for chaining purposes.</returns>
    public Message OnO365ConnectorCardAction(O365ConnectorCardActionHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
    {
        _app.AddRoute(InvokeRouteBuilder.Create()
            .WithName(Microsoft.Teams.Api.Activities.Invokes.Name.ExecuteAction)
            .WithChannelId(_channelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
            .WithHandler(async (turnContext, turnState, cancellationToken) =>
            {
                Microsoft.Teams.Api.O365.ConnectorCardActionQuery query = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.O365.ConnectorCardActionQuery>(turnContext.Activity.Value) ?? new();
                await handler(turnContext, turnState, query, cancellationToken).ConfigureAwait(false);
                await TeamsAgentExtension.SetResponse(turnContext).ConfigureAwait(false);
            })
            .WithOAuthHandlers(autoSignInHandlers)
            .Build());

        return this;
    }
}
