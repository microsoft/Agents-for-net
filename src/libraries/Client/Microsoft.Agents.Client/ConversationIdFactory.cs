// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Storage;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Client
{
    /// <summary>
    /// A <see cref="ConversationIdFactory"/> that uses <see cref="IStorage"/> for backing.
    /// and retrieve <see cref="AgentConversationReference"/> instances.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ConversationIdFactory"/> class.
    /// </remarks>
    /// <param name="storage">
    /// <see cref="IStorage"/> instance to write and read <see cref="AgentConversationReference"/> with.
    /// </param>
    internal class ConversationIdFactory(IStorage storage) : IConversationIdFactory
    {
        private readonly IStorage _storage = storage ?? throw new ArgumentNullException(nameof(storage));

        /// <summary>
        /// Creates a new <see cref="AgentConversationReference"/>.
        /// </summary>
        /// <param name="options">Creation options to use when creating the <see cref="AgentConversationReference"/>.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>ID of the created <see cref="AgentConversationReference"/>.</returns>
        public async Task<string> CreateConversationIdAsync(
            ConversationIdFactoryOptions options,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(options);

            // Create the storage key based on the options.
            var conversationReference = options.Activity.GetConversationReference();

            var channelConversationId = Guid.NewGuid().ToString();

            // Create the ChannelConversationReference instance.
            var channelConversationReference = new AgentConversationReference
            {
                ConversationReference = conversationReference,
                OAuthScope = options.FromOAuthScope,
                AgentName = options.Agent.Name
            };

            // Store the ChannelConversationReference using the conversationId as a key.
            var channelConversationInfo = new Dictionary<string, object>
            {
                {
                    channelConversationId, channelConversationReference
                }
            };

            await _storage.WriteAsync(channelConversationInfo, cancellationToken).ConfigureAwait(false);

            // Return the generated channelConversationId (that will be also used as the conversation ID to call the channel).
            return channelConversationId;
        }

        /// <summary>
        /// Retrieve a <see cref="AgentConversationReference"/> with the specified ID.
        /// </summary>
        /// <param name="channelConversationId">The ID of the <see cref="AgentConversationReference"/> to retrieve.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><see cref="AgentConversationReference"/> for the specified ID; null if not found.</returns>
        public async Task<AgentConversationReference> GetAgentConversationReferenceAsync(
            string channelConversationId,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(channelConversationId);

            // Get the ChannelConversationReference from storage for the given channelConversationId.
            var channelConversationInfo = await _storage
                .ReadAsync(new[] { channelConversationId }, cancellationToken)
                .ConfigureAwait(false);

            if (channelConversationInfo.TryGetValue(channelConversationId, out var channelConversationReference))
            {
                return ProtocolJsonSerializer.ToObject<AgentConversationReference>(channelConversationReference);
            }

            return null;
        }

        /// <summary>
        /// Deletes the <see cref="AgentConversationReference"/> with the specified ID.
        /// </summary>
        /// <param name="channelConversationId">The ID of the <see cref="AgentConversationReference"/> to be deleted.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task to complete the deletion operation asynchronously.</returns>
        public async Task DeleteConversationReferenceAsync(
            string channelConversationId,
            CancellationToken cancellationToken)
        {
            // Delete the ChannelConversationReference from storage.
            await _storage.DeleteAsync(new[] { channelConversationId }, cancellationToken).ConfigureAwait(false);
        }
    }
}
