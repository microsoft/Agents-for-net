// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Storage;
using System;

namespace Microsoft.Agents.State
{
    /// <summary>
    /// Defines a state management object for conversation state.
    /// </summary>
    /// <remarks>
    /// Conversation state is available in any turn in a specific conversation, regardless of user,
    /// such as in a group conversation.
    /// </remarks>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ConversationState"/> class.
    /// </remarks>
    /// <param name="storage">The storage layer to use.</param>
    public class ConversationState(IStorage storage) : BotState(storage, nameof(ConversationState))
    {
        /// <summary>
        /// Gets the key to use when reading and writing state to and from storage.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <returns>The storage key.</returns>
        /// <remarks>
        /// Conversation state includes the channel ID and conversation ID as part of its storage key.
        /// </remarks>
        protected override string GetStorageKey(ITurnContext turnContext)
        {
            var channelId = turnContext.Activity.ChannelId ?? throw new InvalidOperationException("invalid activity-missing channelId");
            var conversationId = turnContext.Activity.Conversation?.Id ?? throw new InvalidOperationException("invalid activity-missing Conversation.Id");
            return $"{channelId}/conversations/{conversationId}";
        }
    }
}
