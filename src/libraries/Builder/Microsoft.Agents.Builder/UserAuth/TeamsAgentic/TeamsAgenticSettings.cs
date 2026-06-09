// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Builder.UserAuth.TeamsAgentic
{
    /// <summary>
    /// Settings for TeamsAgenticAuthorization.
    /// </summary>
    public class TeamsAgenticSettings
    {
        public static readonly TimeSpan DefaultTimeoutValue = TimeSpan.FromMinutes(15);

        /// <summary>
        /// The AAD scopes for authentication. Only one resource is allowed in the scopes.
        /// </summary>
        public string[] Scopes { get; set; } = [];

        /// <summary>
        /// Name of the IConnections token provider for the agent's identity.
        /// If not specified, the connection is resolved from the turn via
        /// IConnections.GetTokenProvider(ClaimsIdentity, IActivity).
        /// </summary>
        public string ConnectionName { get; set; }

        /// <summary>
        /// Name of the IConnections entry for a regular (non-blueprint) app registration
        /// that can perform interactive OAuth. Used as the client_id in the authorize URL
        /// and for exchanging the authorization code. Required when the agent's primary
        /// app registration is a blueprint/agent identity.
        /// If not set, falls back to ConnectionName.
        /// </summary>
        public string OAuthConnectionName { get; set; }

        /// <summary>
        /// The redirect URI for the OAuth callback endpoint hosted by the bot.
        /// This should be the bot's publicly accessible URL + the callback path (e.g., "https://mybot.azurewebsites.net/auth/callback").
        /// </summary>
        public string RedirectUri { get; set; }

        public int? Timeout { get; set; } = (int)DefaultTimeoutValue.TotalMilliseconds;

        public string InvalidSignInRetryMessage { get; set; } = "Please click the sign in button to continue.";
        public int InvalidSignInRetryMax { get; set; } = 2;

        /// <summary>
        /// Initializes the class
        /// </summary>
        /// <param name="oAuthConnectionName">Name of the IConnections entry for interactive OAuth.</param>
        /// <param name="scopes">The AAD scopes for authentication.</param>
        /// <param name="redirectUri">The redirect URI for the OAuth callback endpoint hosted by the bot.</param>
        /// <param name="connectionName">Name of the IConnections entry for the agent's identity. Optional.</param>
        /// <param name="timeout">Number of milliseconds to wait for the user to authenticate.</param>
        public TeamsAgenticSettings(string oAuthConnectionName, string[] scopes, string redirectUri = null, string connectionName = null, int timeout = 900000)
        {
            OAuthConnectionName = oAuthConnectionName;
            Scopes = scopes;
            RedirectUri = redirectUri;
            ConnectionName = connectionName;
            Timeout = timeout;
        }
    }
}
