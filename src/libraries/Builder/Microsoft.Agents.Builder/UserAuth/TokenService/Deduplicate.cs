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
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models.Activities;
using Microsoft.Agents.Connector.Types;

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
    internal class Deduplicate(OAuthSettings settings, IStorage storage)
    {

        /// <summary>
        /// Deduplicates exchange token requests.
        /// </summary>
        /// <param name="turnContext"></param>
        /// <param name="storage"></param>
        /// <param name="connectionName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>true to continue processing the turn.</returns>
        public async Task<bool> ProceedWithExchangeAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            if (ShouldDeduplicate(turnContext))
            {
                // If the TokenExchange is NOT successful, the InvokeResponse will have already been sent.
                if (!await IsTokenExchangeableAsync(turnContext, settings.AzureBotOAuthConnectionName, storage, cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }

                // Only one token exchange should proceed from here. Deduplication is performed second because in the case
                // of failure due to consent required, every caller needs to receive the InvokeResponse
                if (await IsDuplicateTokenExchangeIdAsync(turnContext, storage, cancellationToken).ConfigureAwait(false))
                {
                    // Stop if the token has already been exchanged.
                    return false;
                }
            }

            return true;
        }

        private static bool ShouldDeduplicate(ITurnContext turnContext)
        {
            // Teams
            if (turnContext.Activity.ChannelId.IsParentChannel(Channels.Msteams)
                && string.Equals(SignInConstants.TokenExchangeOperationName, (turnContext.Activity as IInvokeActivity)?.Name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // SharePoint
            if (turnContext.Activity.ChannelId.IsParentChannel(Channels.M365)
                && string.Equals(SignInConstants.SharePointTokenExchange, (turnContext.Activity as IInvokeActivity)?.Name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        // true if the token can be exchanged, otherwise OAuth can't proceed.
        // In the case of ConsentRequired, end user needs to consent and Teams will send another TokenExchange Invoke.
        private static async Task<bool> IsTokenExchangeableAsync(ITurnContext turnContext, string connectionName, IStorage storage, CancellationToken cancellationToken)
        {
            TokenResponse tokenExchangeResponse = null;
            var tokenExchangeRequest = ProtocolJsonSerializer.ToObject<TokenExchangeInvokeRequest>((turnContext.Activity as IInvokeActivity)?.Value);

            try
            {
                // Try to exchange the token
                tokenExchangeResponse = await UserTokenClientWrapper.ExchangeTokenAsync(
                    turnContext,
                    connectionName,
                    new TokenExchangeRequest { Token = tokenExchangeRequest.Token },
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // The token cannot be exchanged.
                // ExchangeTokenAsync will throws for ConsentRequired (400), or other non-success.  We need to know which we are getting.
                bool isConsentRequired = ex as ErrorResponseException != null && ((ErrorResponseException)ex).Body.Error.Code.Equals(Error.ConsentRequiredCode);

                // If this isn't a consent error, we still can only proceed for one Exchange request (multiple clients)
                if (!isConsentRequired && await IsDuplicateTokenExchangeIdAsync(turnContext, storage, cancellationToken).ConfigureAwait(false))
                {
                    // This is the duplicate Invoke.  Bail now.  IsDuplicateTokenExchangeIdAsync will have sent the expected InvokeResponse.
                    throw new DuplicateExchangeException();
                }

                // This will be the first response to Teams:
                //    ConsentRequired: PreconditionFailed
                //    Error: BadRequest
                var exchangeResponse = new TokenExchangeInvokeResponse
                {
                    Id = tokenExchangeRequest.Id,
                    ConnectionName = connectionName,
                    FailureDetail = ex.Message,
                };

                await turnContext.SendActivityAsync(
                    new InvokeResponseActivity
                    {
                        Value = new InvokeResponse
                        {
                            Status = isConsentRequired ? (int)HttpStatusCode.PreconditionFailed : (int)HttpStatusCode.BadRequest,
                            Body = exchangeResponse
                        },
                    },
                    cancellationToken).ConfigureAwait(false);

                if (!isConsentRequired)
                {
                    throw new AuthException(exchangeResponse.FailureDetail, AuthExceptionReason.Exception, ex);
                }

                return false;
            }

            return true;
        }

        // true if the Invoke is a duplicate
        private static async Task<bool> IsDuplicateTokenExchangeIdAsync(ITurnContext turnContext, IStorage storage, CancellationToken cancellationToken)
        {
            var id = (turnContext.Activity as IInvokeActivity)?.Value.ToJsonElements()["id"].ToString();

            // Create a StoreItem with Etag of the unique 'signin/tokenExchange' request
            var storeItem = new TokenStoreItem
            {
                ETag = id,
            };

            var storeItems = new Dictionary<string, object> { { TokenStoreItem.GetStorageKey(turnContext, id), storeItem } };
            try
            {
                // Writing the IStoreItem with ETag of unique id will succeed only once
                // NOTE: There is not, and has never been, a way to clean this up.  Possibly trap when the entire
                // process is done and delete?
                await storage.WriteAsync(storeItems, cancellationToken).ConfigureAwait(false);
            }
            catch (EtagException)
            {
                // Do NOT proceed processing this message, some other thread or machine already has processed it.

                // Send 200 invoke response.
                await turnContext.SendActivityAsync(
                    new InvokeResponseActivity(),
                    cancellationToken).ConfigureAwait(false);
                return true;
            }

            return false;
        }

        private class TokenStoreItem : IStoreItem
        {
            public string ETag { get; set; }

            public static string GetStorageKey(ITurnContext turnContext, string id)
            {
                return $"oauth/{turnContext.Activity.ChannelId}/{turnContext.Activity.Conversation?.Id}/{id}";
            }
        }
    }
}
