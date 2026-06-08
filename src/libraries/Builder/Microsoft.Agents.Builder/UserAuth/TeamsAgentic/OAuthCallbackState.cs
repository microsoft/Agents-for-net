// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using System;

namespace Microsoft.Agents.Extensions.Teams.App.UserAuth
{
    /// <summary>
    /// Stores pending OAuth state between when the sign-in card is sent
    /// and when the OAuth callback is received.
    /// </summary>
    internal class OAuthCallbackState : IStoreItem
    {
        /// <summary>
        /// PKCE code verifier to use when exchanging the authorization code.
        /// </summary>
        public string CodeVerifier { get; set; }

        /// <summary>
        /// The MSAL home account ID ({aadObjectId}.{tenantId}) for caching the token.
        /// </summary>
        public string HomeAccountId { get; set; }

        /// <summary>
        /// The tenant ID for the token request.
        /// </summary>
        public string TenantId { get; set; }

        /// <summary>
        /// The auth handler name (used for storage key correlation).
        /// </summary>
        public string AuthName { get; set; }

        /// <summary>
        /// When this pending state expires (should be cleaned up after).
        /// </summary>
        public DateTime Expires { get; set; }

        /// <summary>
        /// The scopes requested.
        /// </summary>
        public string[] Scopes { get; set; }

        /// <summary>
        /// The redirect URI that was used (for code exchange).
        /// </summary>
        public string RedirectUri { get; set; }

        /// <summary>
        /// The connection name for MSAL.
        /// </summary>
        public string ConnectionName { get; set; }

        /// <summary>
        /// The bot/agent app client ID (used for creating proactive ClaimsIdentity).
        /// </summary>
        public string BotClientId { get; set; }

        /// <summary>
        /// The conversation reference for sending a proactive invoke back to the bot.
        /// </summary>
        public ConversationReference ConversationReference { get; set; }

        public string ETag { get; set; }
    }
}
