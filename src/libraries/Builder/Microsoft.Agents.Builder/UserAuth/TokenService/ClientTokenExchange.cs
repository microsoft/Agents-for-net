// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Storage;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Connector;

namespace Microsoft.Agents.Builder.UserAuth.TokenService
{
    /// <summary>
    /// If the activity name is signin/tokenExchange, this will attempt to
    /// exchange the token, and deduplicate the incoming call, ensuring only one
    /// exchange request is processed.
    /// </summary>
    /// <remarks>
    /// This is only for Teams or SharePoint channels.
    /// If a user is signed into multiple Teams clients, the Agent could receive a
    /// "signin/tokenExchange" from each client. Each token exchange request for a
    /// specific user login will have an identical Activity.Value.Id.
    /// 
    /// Only one of these token exchange requests should be processed by the Agent.
    /// The others return <see cref="HttpStatusCode.PreconditionFailed"/>.
    /// For a distributed Agent in production, this requires a distributed storage
    /// ensuring only one token exchange is processed. This supports
    /// CosmosDb storage found in Microsoft.Agents.Storage.CosmosDb, 
    /// Microsoft.Agents.Storage.Blobs, or MemoryStorage for
    /// local development. IStorage's ETag implementation for token exchange activity
    /// deduplication.
    /// </remarks>
    internal class ClientTokenExchange
    {
        private readonly OAuthSettings _settings;
        private readonly IStorage _storage;

        public ClientTokenExchange(OAuthSettings oauthSettings, IStorage storage)
        {
            _settings = oauthSettings;
            _storage = storage;
        }

        /// <summary>
        /// Deduplicates exchange token requests.
        /// </summary>
        /// <param name="turnContext"></param>
        /// <param name="storage"></param>
        /// <param name="connectionName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>true to continue processing the turn.</returns>
        public async Task<bool> DedupeAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            if (ShouldExchange(turnContext))
            {
                // If the TokenExchange is NOT successful, the response will have already been sent by ExchangedTokenAsync
                if (!await ExchangedTokenAsync(turnContext, _settings.AzureBotOAuthConnectionName, cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }

                // Only one token exchange should proceed from here. Deduplication is performed second because in the case
                // of failure due to consent required, every caller needs to receive the 
                if (!await DeduplicatedTokenExchangeIdAsync(turnContext, _storage, cancellationToken).ConfigureAwait(false))
                {
                    // If the token is not exchangeable, do not process this activity further.
                    return false;
                }
            }

            return true;
        }

        private static bool ShouldExchange(ITurnContext turnContext)
        {
            // Teams
            if (string.Equals(Channels.Msteams, turnContext.Activity.ChannelId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(SignInConstants.TokenExchangeOperationName, turnContext.Activity.Name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // SharePoint
            if (string.Equals(Channels.M365, turnContext.Activity.ChannelId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(SignInConstants.SharePointTokenExchange, turnContext.Activity.Name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static async Task<bool> DeduplicatedTokenExchangeIdAsync(ITurnContext turnContext, IStorage storage, CancellationToken cancellationToken)
        {
            // Create a StoreItem with Etag of the unique 'signin/tokenExchange' request
            var storeItem = new TokenStoreItem
            {
                ETag = turnContext.Activity.Value.ToJsonElements()["id"].ToString(),
            };

            var storeItems = new Dictionary<string, object> { { TokenStoreItem.GetStorageKey(turnContext), storeItem } };
            try
            {
                // Writing the IStoreItem with ETag of unique id will succeed only once
                await storage.WriteAsync(storeItems, cancellationToken).ConfigureAwait(false);
            }
            catch (EtagException)
            {
                // Do NOT proceed processing this message, some other thread or machine already has processed it.

                // Send 200 invoke response.
                await SendInvokeResponseAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);
                return false;
            }

            return true;
        }

        private static async Task SendInvokeResponseAsync(ITurnContext turnContext, object body = null, HttpStatusCode httpStatusCode = HttpStatusCode.OK, CancellationToken cancellationToken = default)
        {
            await turnContext.SendActivityAsync(
                new Activity
                {
                    Type = ActivityTypes.InvokeResponse,
                    Value = new InvokeResponse
                    {
                        Status = (int)httpStatusCode,
                        Body = body,
                    },
                }, cancellationToken).ConfigureAwait(false);
        }

        private async Task<bool> ExchangedTokenAsync(ITurnContext turnContext, string connectionName, CancellationToken cancellationToken)
        {
            TokenResponse tokenExchangeResponse = null;
            var tokenExchangeRequest = ProtocolJsonSerializer.ToObject<TokenExchangeInvokeRequest>(turnContext.Activity.Value);

            try
            {
                var userTokenClient = turnContext.Services.Get<IUserTokenClient>();
                if (userTokenClient != null)
                {
                    tokenExchangeResponse = await UserTokenClientWrapper.ExchangeTokenAsync(
                        turnContext,
                        connectionName,
                        new TokenExchangeRequest { Token = tokenExchangeRequest.Token },
                        cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    throw new NotSupportedException("Token Exchange is not supported by the current adapter.");
                }
            }
            catch
            {
                // Ignore Exceptions
                // If token exchange failed for any reason, tokenExchangeResponse above stays null,
                // and hence we send back a failure invoke response to the caller.
            }

            if (string.IsNullOrEmpty(tokenExchangeResponse?.Token))
            {
                // The token could not be exchanged (which could be due to a consent requirement)
                // Notify the sender that PreconditionFailed so they can respond accordingly.

                var invokeResponse = new TokenExchangeInvokeResponse
                {
                    Id = tokenExchangeRequest.Id,
                    ConnectionName = connectionName,
                    FailureDetail = "The Agent is unable to exchange token. Proceed with regular login.",
                };

                await SendInvokeResponseAsync(turnContext, invokeResponse, HttpStatusCode.PreconditionFailed, cancellationToken).ConfigureAwait(false);

                return false;
            }

            return true;
        }

        private class TokenStoreItem : IStoreItem
        {
            public string ETag { get; set; }

            public static string GetStorageKey(ITurnContext turnContext)
            {
                var activity = turnContext.Activity;
                var channelId = activity.ChannelId ?? throw new InvalidOperationException("invalid activity-missing channelId");
                var conversationId = activity.Conversation?.Id ?? throw new InvalidOperationException("invalid activity-missing Conversation.Id");

                var value = activity.Value.ToJsonElements();
                if (value == null || !value.ContainsKey("id"))
                {
                    throw new InvalidOperationException("Invalid signin/tokenExchange. Missing activity.Value.Id.");
                }

                return $"oauth/{channelId}/{conversationId}/{value["id"]}";
            }
        }
    }
}
