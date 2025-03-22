// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Client
{
    /// <summary>
    /// Represents a host the contains a collection of Agents for Agent-to-Agent communication.
    /// </summary>
    public interface IAgentHost
    {
        /// <summary>
        /// The endpoint to use in Activity.ServiceUrl if unspecified in an Agents settings.
        /// </summary>
        Uri DefaultResponseEndpoint { get; set; }

        string HostClientId { get; set; }

        /// <summary>
        /// Returns a list of configured Agents.
        /// </summary>
        IList<IAgentInfo> GetAgents();

        /// <summary>
        /// Gets the Agent Client used to send Activities to another Agent.
        /// </summary>
        /// <param name="agentName">The name of the Agent</param>
        IAgentClient GetClient(string agentName);

        /// <summary>
        /// Sends an activity to an Agent.
        /// </summary>
        /// <remarks>
        /// This is used for Activity.DeliverMode == 'normal'.  In order to get the asynchronous replies from the Agent, the
        /// <see cref="AgentResponsesExtension.OnAgentReply"/> handler must be set on the AgentApplication.
        /// </remarks>
        /// <remarks>
        /// This will not properly handle Invoke or ExpectReplies requests as it's doesn't return a value.  Use <see cref="GetClient(string)"/> and 
        /// use the returned <see cref="IAgentClient"/> directly for those.
        /// </remarks>
        /// <param name="agentName">An Agent name from configuration.</param>
        /// <param name="agentConversationId"><see cref="GetOrCreateConversationAsync"/> or <see cref="GetConversation"/></param>
        /// <param name="activity"></param>
        /// <param name="cancellationToken"></param>
        /// <exception cref="ArgumentException">If the specified agentName is null or not found.</exception>
        Task SendToAgent(string agentName, string agentConversationId, IActivity activity, CancellationToken cancellationToken = default);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="agentName">An Agent name from configuration.</param>
        /// <param name="agentConversationId"><see cref="GetOrCreateConversationAsync"/> or <see cref="GetConversation"/></param>
        /// <param name="activity"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        IAsyncEnumerable<object> SendToAgentStreamedAsync(string agentName, string agentConversationId, IActivity activity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the conversationId for an existing conversation for a Agent, relative to the current Turns Conversation.
        /// </summary>
        /// <remarks>
        /// IAgentHost currently only supports a single active conversation per Agent per Turn Conversation.
        /// </remarks>
        /// <param name="turnContext"></param>
        /// <param name="conversationState"></param>
        /// <param name="agentName"></param>
        /// <returns>conversationId for an existing conversation, or null.</returns>
        string GetConversation(ITurnContext turnContext, ConversationState conversationState, string agentName);

        /// <summary>
        /// Returns a list of all Agent conversations for the current Turns Conversation.
        /// </summary>
        /// <param name="turnContext"></param>
        /// <param name="conversationState">Typically from <see cref="ITurnState.Conversation"/></param>
        /// <returns>Non-null list of <see cref="AgentConversation"/>.</returns>
        IList<AgentConversation> GetConversations(ITurnContext turnContext, ConversationState conversationState);

        /// <summary>
        /// Returns the existing conversation for an Agent, or creates a new one.
        /// </summary>
        /// <remarks>
        /// IAgentHost currently only supports a single active conversation per Agent per Turn Conversation.
        /// </remarks>
        /// <param name="turnContext"></param>
        /// <param name="conversationState">Typically from <see cref="ITurnState.Conversation"/></param>
        /// <param name="agentName">An Agent name from configuration.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<string> GetOrCreateConversationAsync(ITurnContext turnContext, ConversationState conversationState, string agentName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes the indicated conversation.
        /// </summary>
        /// <remarks>
        /// Only the bot knows when a conversation is done.  All effort should be made to remove conversations as otherwise the persisted conversations accumulate.
        /// A received Activity of type EndOfConversation is one instance where the conversation should be deleted.
        /// </remarks>
        /// <param name="agentConversationId">A conversationId return from <see cref="GetConversation"/> or <see cref="GetOrCreateConversationAsync"/>.</param>
        /// <param name="conversationState">Typically from <see cref="ITurnState.Conversation"/></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task DeleteConversationAsync(string agentConversationId, ConversationState conversationState, CancellationToken cancellationToken = default);

        /// <summary>
        /// Ends all active conversations for the Turn conversation.
        /// </summary>
        /// <remarks>
        /// This means for all conversation with other Agents for the current ITurnContext conversation.  This also sends an
        /// EndOfConversations to each Agent.
        /// </remarks>
        /// <param name="turnContext"></param>
        /// <param name="conversationState"></param>
        /// <param name="cancellationToken"></param>
        Task EndAllConversations(ITurnContext turnContext, ConversationState conversationState, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the conversation information for the specified Agent conversation.
        /// </summary>
        /// <param name="agentConversationId"></param>
        /// <param name="cancellationToken"></param>
        Task<AgentConversationReference> GetConversationReferenceAsync(string agentConversationId, CancellationToken cancellationToken = default);
    }
}
