// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System.Threading.Tasks;
using System.Threading;
using System;
using Microsoft.Agents.Core.Models.Activities;

namespace Microsoft.Agents.Client
{
    public delegate Task AgentResponseHandler(ITurnContext turnContext, ITurnState turnState, ChannelConversationReference reference, IActivity agentActivity, CancellationToken cancellationToken);

    /// <summary>
    /// This AgentApplication Extension provides features to help handle agent-to-agent communication.
    /// </summary>
    /// <param name="agentApplication"></param>
    /// <param name="agentHost"></param>
    public class AgentResponses(AgentApplication agentApplication, IAgentHost agentHost) : IAgentExtension
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
                var channelResponse = ProtocolJsonSerializer.ToObject<AdapterChannelResponseHandler.ChannelReply>((turnContext.Activity as IEventActivity)?.Value);
                await handler(turnContext, turnState, channelResponse.ChannelConversationReference, channelResponse.Activity, cancellationToken).ConfigureAwait(false);
            }

            agentApplication.OnActivity(
                (turnContext, CancellationToken) =>
                {
                    if (turnContext.Activity is not IEventActivity eventActivity)
                    {
                        return Task.FromResult(false);
                    }
                    else
                    {
                        return Task.FromResult(string.Equals(AdapterChannelResponseHandler.ChannelReplyEventName, eventActivity.Name, StringComparison.OrdinalIgnoreCase));
                    }
                },
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
        /// <param name="additionalHandler">An optional additional RouteHandler that is called prior to ConversationState being deleted.</param>
        /// <param name="rank"></param>
        public void AddDefaultEndOfConversationHandling(RouteHandler additionalHandler = null, ushort rank = RouteRank.Unspecified)
        {
            async Task eocHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                // Give the additionalHandler a chance to act on the EOC before default handling nukes it.
                if (additionalHandler != null)
                {
                    await additionalHandler(turnContext, turnState, cancellationToken).ConfigureAwait(false);
                }

                // This tells each active Agent that the conversation is over, and remove it from state.
                await agentHost.EndAllConversations(turnContext, cancellationToken).ConfigureAwait(false);
            }

            agentApplication.OnActivity(
                (turnContext, CancellationToken) => Task.FromResult(turnContext.Activity.IsType(ActivityTypes.EndOfConversation)),
                eocHandler,
                rank);


            // On error, end all active Agent conversations and delete ConversationState.
            async Task errorHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                await eocHandler(turnContext, turnState, CancellationToken.None).ConfigureAwait(false);

                // The default behavior is that in the event of an uncaught exception, things are in the weeds, and
                // best to just reset the ConversationState.
                await turnState.Conversation.DeleteStateAsync(turnContext, cancellationToken).ConfigureAwait(false);
            }

            // This adds an AgentApplication.OnTurnError to handle cleaning up when an uncaught exception occurs.
            agentApplication.OnTurnError(async (turnContext, turnState, exception, cancellationToken) => await errorHandler(turnContext, turnState, cancellationToken).ConfigureAwait(false));
        }

#if !NETSTANDARD
        public ChannelId ChannelId { get; init; } = "*";
#else
        public ChannelId ChannelId { get; set; } = "*";
#endif

        public void AddRoute(AgentApplication agentApplication, RouteSelector routeSelector, RouteHandler routeHandler, bool isInvokeRoute = false, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            agentApplication.AddRoute(routeSelector, routeHandler, isInvokeRoute, rank, autoSignInHandlers, isAgenticOnly);
        }
    }
}
