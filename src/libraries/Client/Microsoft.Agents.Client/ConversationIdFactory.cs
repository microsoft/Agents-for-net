// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <inheritdoc/>
        public Task<string> CreateConversationIdAsync(ITurnContext turnContext, string hostAppId, string botAlias, CancellationToken cancellationToken)
        {
            return CreateConversationIdAsync(GetConversationFactoryOptions(turnContext, hostAppId, botAlias), cancellationToken);
        }

        /// <inheritdoc/>
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
                OAuthScope = options.FromBotOAuthScope,
                BotConversationId = botConversationId,
                FromAppId = options.FromBotId,
                ToBotName = options.ToBotName,
            };

            // Store the BotConversationReference using the conversationId as a key.
            var botConversationInfo = new Dictionary<string, object>
            {
                {
                    botConversationId, botConversationReference
                },
                {
                    GetParentKey(options, conversationReference), botConversationReference
                }
            };

            await _storage.WriteAsync(botConversationInfo, cancellationToken).ConfigureAwait(false);

            // Return the generated botConversationId (that will be also used as the conversation ID to call the bot).
            return botConversationId;
        }

        /// <inheritdoc/>
        public async Task<BotConversationReference> GetBotConversationReferenceAsync(
            string botConversationId,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(botConversationId);

            // Get the BotConversationReference from storage for the given botConversationId.
            var botConversationInfo = await _storage
                .ReadAsync([botConversationId], cancellationToken)
                .ConfigureAwait(false);

            if (botConversationInfo.TryGetValue(botConversationId, out var botConversationReference))
            {
                return ProtocolJsonSerializer.ToObject<BotConversationReference>(botConversationReference);
            }

            return null;
        }

        public Task<string> GetBotConversationIdAsync(ITurnContext turnContext, string hostAppId, string botAlias, CancellationToken cancellationToken)
        {
            return GetBotConversationIdAsync(GetConversationFactoryOptions(turnContext, hostAppId, botAlias), cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<string> GetBotConversationIdAsync(ConversationIdFactoryOptions options, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(options);

            var result = await _storage
                .ReadAsync([GetParentKey(options)], cancellationToken)
                .ConfigureAwait(false);

            if (result?.Count == 0)
            {
                return null;
            }

            return ProtocolJsonSerializer.ToObject<BotConversationReference>(result.First().Value).BotConversationId;
        }

        /// <inheritdoc/>
        public async Task DeleteConversationReferenceAsync(
            string botConversationId,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(botConversationId);

            // Delete the BotConversationReference from storage.
            var botConversation = await GetBotConversationReferenceAsync(botConversationId.ToString(), cancellationToken).ConfigureAwait(false);
            if (botConversation != null)
            {
                await _storage.DeleteAsync([botConversationId, $"{botConversation.ToBotName}\\{botConversation.FromAppId}\\{botConversation.ConversationReference.Conversation.Id}"], cancellationToken).ConfigureAwait(false);
            }
        }

        private static string GetParentKey(ConversationIdFactoryOptions options, ConversationReference conversation = null)
        {
            var conversationReference = conversation ?? options.Activity.GetConversationReference();
            return $"{options.ToBotName}\\{options.FromBotId}\\{conversationReference.Conversation.Id}";
        }

        private ConversationIdFactoryOptions GetConversationFactoryOptions(ITurnContext turnContext, string hostAppId, string botAlias)
        {
            return new ConversationIdFactoryOptions
            {
                FromBotOAuthScope = turnContext.TurnState.Get<string>(TurnStateKeys.OAuthScopeKey),
                FromBotId = hostAppId,
                ToBotName = botAlias,
                Activity = turnContext.Activity
            };
        }
    }
}
