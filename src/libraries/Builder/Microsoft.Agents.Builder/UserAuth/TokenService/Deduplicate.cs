// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

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
    /// </remarks>
    internal class Deduplicate(IStorage storage, ILogger logger = null)
    {
        private ILogger _logger = logger ?? NullLogger.Instance;

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
                // Only one token exchange should proceed from here. Deduplication is performed second because in the case
                // of failure due to consent required, every caller needs to receive the InvokeResponse
                if (await IsDuplicateTokenExchangeAsync(turnContext, storage, cancellationToken).ConfigureAwait(false))
                {
                    // Do NOT proceed processing this message, some other thread or machine already has processed it.
                    // But we must send an InvokeResponse
                    await turnContext.SendActivityAsync(
                        new Activity
                        {
                            Type = ActivityTypes.InvokeResponse,
                            Value = new InvokeResponse
                            {
                                Status = (int)HttpStatusCode.OK,
                            },
                        },
                        cancellationToken).ConfigureAwait(false);

                    // Stop if the token has already been exchanged.
                    return false;
                }
            }

            return true;
        }

        public Task DeleteTokenExchangeAsync(ITurnContext turnContext)
        {
            var key = TokenStoreItem.GetStorageKey(turnContext);
            return storage.DeleteAsync([key]);
        }

        private static bool ShouldDeduplicate(ITurnContext turnContext)
        {
            // Teams
            if (turnContext.Activity.ChannelId.IsParentChannel(Channels.Msteams)
                && string.Equals(SignInConstants.TokenExchangeOperationName, turnContext.Activity.Name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // SharePoint
            if (turnContext.Activity.ChannelId == Channels.M365
                && string.Equals(SignInConstants.SharePointTokenExchange, turnContext.Activity.Name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        // true if the Invoke is a duplicate
        private async Task<bool> IsDuplicateTokenExchangeAsync(ITurnContext turnContext, IStorage storage, CancellationToken cancellationToken)
        {
            try
            {
                var key = TokenStoreItem.GetStorageKey(turnContext);
                if (storage is IStorageExt storageExt)
                {
                    await storageExt.WriteAsync(new Dictionary<string, object>() { { key, new TokenStoreItem() } }, new StorageWriteOptions(true), cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    _logger.LogWarning("Unable to check if token exchange is duplicated because storage does not implement IStorageExt.");
                }
                return false;
            }
            catch (ItemExistsException)
            {
                return true;
            }
            catch (EtagException)
            {
                return true;
            }
        }

        private class TokenStoreItem : IStoreItem
        {
            public string ETag { get; set; }

            public static string GetStorageKey(ITurnContext turnContext)
            {
                var channelId = turnContext.Activity.ChannelId ?? throw new InvalidOperationException("invalid activity-missing channelId");
                var userId = turnContext.Activity.From?.Id ?? throw new InvalidOperationException("invalid activity-missing From.Id");
                return $"oauth/deduplicate/{channelId}/{userId}";
            }
        }
    }
}
