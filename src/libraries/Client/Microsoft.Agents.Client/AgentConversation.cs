// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Client
{
    /// <summary>
    /// Contains the Agent information for active Conversations.
    /// </summary>
    public class AgentConversation
    {
        public string AgentName { get; internal set; }

        /// <summary>
        /// This is the conversationId created with <see cref="IAgentHost.GetOrCreateConversationAsync(BotBuilder.ITurnContext, BotBuilder.State.ConversationState, string, System.Threading.CancellationToken)"/>.
        /// </summary>
        public string AgentConversationId { get; internal set; }
    }
}
