// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Client
{
    /// <summary>
    /// Defines the interface of a factory that is used to create unique conversation IDs for Agent conversations.
    /// </summary>
    internal interface IConversationIdFactory
    {
        /// <summary>
        /// Creates a conversation id for a Agent conversation.
        /// </summary>
        /// <param name="options">A <see cref="ConversationIdFactoryOptions"/> instance containing parameters for creating the conversation ID.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A unique conversation ID used to communicate with an Agent.</returns>
        /// <remarks>
        /// It should be possible to use the returned string on a request URL and it should not contain special characters. 
        /// </remarks>
        Task<string> CreateConversationIdAsync(ConversationIdFactoryOptions options, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the <see cref="ChannelConversationReference"/> used during <see cref="CreateConversationIdAsync(ConversationIdFactoryOptions,System.Threading.CancellationToken)"/> for an Agent conversation.
        /// </summary>
        /// <param name="agentConversationId">A conversationId for an Agent created using <see cref="CreateConversationIdAsync(ConversationIdFactoryOptions,System.Threading.CancellationToken)"/>.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The caller's <see cref="ConversationReference"/> for a conversationId, with originatingAudience. Null if not found.</returns>
        Task<ChannelConversationReference> GetAgentConversationReferenceAsync(string agentConversationId, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes a <see cref="ConversationReference"/>.
        /// </summary>
        /// <param name="agentConversationId">A conversationId for an Agent created using <see cref="CreateConversationIdAsync(ConversationIdFactoryOptions,System.Threading.CancellationToken)"/>.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task DeleteConversationReferenceAsync(string agentConversationId, CancellationToken cancellationToken);
    }
}
