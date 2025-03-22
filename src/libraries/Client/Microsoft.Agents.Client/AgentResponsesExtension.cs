// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace Microsoft.Agents.Client
{
    public delegate Task AgentResponseHandler(ITurnContext turnContext, ITurnState turnState, AgentConversationReference reference, IActivity agentActivity, CancellationToken cancellationToken);

    /// <summary>
    /// This AgentApplication Extension provides features to help handle agent-to-agent communication.
    /// </summary>
    /// <param name="agentApplication"></param>
    /// <param name="agentHost"></param>
    public class AgentResponsesExtension(AgentApplication agentApplication, IAgentHost agentHost) : IAgentExtension
    {
        /// <summary>
        /// Provides a handler for replies from another Agent when an Activity when <see cref="Activity.DeliveryMode"/> is <see cref="DeliveryModes.Normal"/> was used.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <param name="app"></param>
        /// <param name="handler"></param>
        /// <param name="rank"></param>
        public void OnAgentReply(AgentResponseHandler handler, ushort rank = RouteRank.First)
        {
            async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                var channelResponse = ProtocolJsonSerializer.ToObject<AdapterChannelResponseHandler.ChannelReply>(turnContext.Activity.Value);
                await handler(turnContext, turnState, channelResponse.ChannelConversationReference, channelResponse.Activity, cancellationToken).ConfigureAwait(false);
            }

            agentApplication.OnActivity(
                (turnContext, CancellationToken) =>
                    Task.FromResult(string.Equals(ActivityTypes.Event, turnContext.Activity.Type, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(AdapterChannelResponseHandler.ChannelReplyEventName, turnContext.Activity.Name, StringComparison.OrdinalIgnoreCase)),
                routeHandler,
                rank);
        }

        /// <summary>
        /// Adds default handling of EndOfConversation received by the AgentApplication.
        /// </summary>
        /// <remarks>
        /// Called either by the User sending EOC, or in the case of an AgentApplication TurnError.
        /// Default handling is to end all agent-to-agent conversations, and delete ConversationState.
        /// </remarks>
        /// <param name="handler">An optional additional RouteHandler that is called prior to ConversationState being deleted.</param>
        /// <param name="rank"></param>
        public void AddDefaultEndOfConversationHandling(RouteHandler handler = null, ushort rank = RouteRank.Unspecified)
        {
            async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                await agentHost.EndAllConversations(turnContext, turnState.Conversation, cancellationToken).ConfigureAwait(false);
                if (handler != null)
                {
                    await handler(turnContext, turnState, cancellationToken).ConfigureAwait(false);
                }
                await turnState.Conversation.DeleteStateAsync(turnContext, cancellationToken).ConfigureAwait(false);
            }

            agentApplication.OnActivity(
                (turnContext, CancellationToken) => Task.FromResult(string.Equals(ActivityTypes.EndOfConversation, turnContext.Activity.Type, StringComparison.OrdinalIgnoreCase)),
                routeHandler,
                rank);

            // This adds an AgentApplication.OnTurnError to handle cleaning up when an uncaught exception occurs.
            agentApplication.OnTurnError(async (turnContext, turnState, exception, cancellationToken) => await routeHandler(turnContext, turnState, cancellationToken).ConfigureAwait(false));
        }

        public string ChannelId { get; init; } = "*";

        public void AddRoute(AgentApplication agentApplication, RouteSelector routeSelector, RouteHandler routeHandler, bool isInvokeRoute = false)
        {
            agentApplication.AddRoute(routeSelector, routeHandler, isInvokeRoute);
        }
    }
}
