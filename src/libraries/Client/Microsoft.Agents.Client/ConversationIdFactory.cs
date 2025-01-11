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
    /// and retrieve <see cref="BotConversationReference"/> instances.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ConversationIdFactory"/> class.
    /// </remarks>
    /// <param name="storage">
    /// <see cref="IStorage"/> instance to write and read <see cref="BotConversationReference"/> with.
    /// </param>
    public class ConversationIdFactory(IStorage storage) : IConversationIdFactory
    {
        private readonly IStorage _storage = storage ?? throw new ArgumentNullException(nameof(storage));

        /// <summary>
        /// Creates a new <see cref="BotConversationReference"/>.
        /// </summary>
        /// <param name="options">Creation options to use when creating the <see cref="BotConversationReference"/>.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>ID of the created <see cref="BotConversationReference"/>.</returns>
        public async Task<string> CreateConversationIdAsync(
            ConversationIdFactoryOptions options,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(options);

            // Create the storage key based on the options.
            var conversationReference = options.Activity.GetConversationReference();

            var botConversationId = Guid.NewGuid().ToString();

            // Create the BotConversationReference instance.
            var botConversationReference = new BotConversationReference
            {
                ConversationReference = conversationReference,
                OAuthScope = options.FromBotOAuthScope
            };

            // Store the BotConversationReference using the conversationId as a key.
            var botConversationInfo = new Dictionary<string, object>
            {
                {
                    botConversationId, botConversationReference
                }
            };

            await _storage.WriteAsync(botConversationInfo, cancellationToken).ConfigureAwait(false);

            // Return the generated botConversationId (that will be also used as the conversation ID to call the bot).
            return botConversationId;
        }

        /// <summary>
        /// Retrieve a <see cref="BotConversationReference"/> with the specified ID.
        /// </summary>
        /// <param name="botConversationId">The ID of the <see cref="BotConversationReference"/> to retrieve.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><see cref="BotConversationReference"/> for the specified ID; null if not found.</returns>
        public async Task<BotConversationReference> GetBotConversationReferenceAsync(
            string botConversationId,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(botConversationId);

            // Get the BotConversationReference from storage for the given botConversationId.
            var botConversationInfo = await _storage
                .ReadAsync(new[] { botConversationId }, cancellationToken)
                .ConfigureAwait(false);

            if (botConversationInfo.TryGetValue(botConversationId, out var botConversationReference))
            {
                return ProtocolJsonSerializer.ToObject<BotConversationReference>(botConversationReference);
            }

            return null;
        }

        /// <summary>
        /// Deletes the <see cref="BotConversationReference"/> with the specified ID.
        /// </summary>
        /// <param name="botConversationId">The ID of the <see cref="BotConversationReference"/> to be deleted.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task to complete the deletion operation asynchronously.</returns>
        public async Task DeleteConversationReferenceAsync(
            string botConversationId,
            CancellationToken cancellationToken)
        {
            // Delete the BotConversationReference from storage.
            await _storage.DeleteAsync(new[] { botConversationId }, cancellationToken).ConfigureAwait(false);
        }
    }
}
