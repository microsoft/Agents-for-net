// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.BotBuilder.UserAuth.TokenService
{
    /// <summary>
    /// Handles user authentication using The Azure Bot Token Service.
    /// </summary>
    public class OAuthAuthentication : IUserAuthentication
    {
        private readonly OAuthSettings _settings;
        //private readonly OAuthMessageExtensionsAuthentication? _messageExtensionAuth;
        private readonly OAuthBotAuthentication _botAuthentication;

        /// <summary>
        /// Initializes the class
        /// </summary>
        /// <param name="name">The authentication name.</param>
        /// <param name="settings">The settings to initialize the class</param>
        /// <param name="storage">The storage to use.</param>
        public OAuthAuthentication(string name, OAuthSettings settings, IStorage storage) : this(settings, new OAuthBotAuthentication(name, settings, storage))
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        /// Initializes the class
        /// </summary>
        /// <param name="settings">The settings to initialize the class</param>
        /// <param name="botAuthentication">The bot authentication instance</param>
        /// <param name="messageExtensionAuth">The message extension authentication instance</param>
        internal OAuthAuthentication(OAuthSettings settings, OAuthBotAuthentication botAuthentication)
        {
            _settings = settings;
            //_messageExtensionAuth = messageExtensionAuth;
            _botAuthentication = botAuthentication;
        }

        public string Name { get; private set; }

        /// <summary>
        /// Check if the user is signed, if they are then return the token.
        /// </summary>
        /// <param name="turnContext">The turn turnContext.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The token if the user is signed. Otherwise null.</returns>
        public async Task<string?> IsUserSignedInAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            TokenResponse tokenResponse = await GetUserToken(turnContext, _settings.ConnectionName, cancellationToken);

            if (!string.IsNullOrWhiteSpace(tokenResponse?.Token))
            {
                return tokenResponse.Token;
            }

            return null;
        }

        /// <summary>
        /// Sign in current user
        /// </summary>
        /// <param name="turnContext">The turn turnContext</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The sign in response</returns>
        public async Task<TokenResponse> SignInUserAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            /*
            if ((_messageExtensionAuth != null && _messageExtensionAuth.IsValidActivity(turnContext)))
            {
                return await _messageExtensionAuth.AuthenticateAsync(turnContext).ConfigureAwait(false);
            }
            */

            if (_botAuthentication != null && _botAuthentication.IsValidActivity(turnContext))
            {
                return await _botAuthentication.AuthenticateAsync(turnContext, cancellationToken).ConfigureAwait(false);
            }

            throw new AuthException("Incoming activity is not a valid activity to initiate authentication flow.", AuthExceptionReason.InvalidActivity);
        }

        public async Task ResetStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await _botAuthentication.ResetStateAsync(turnContext, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sign out current user
        /// </summary>
        /// <param name="turnContext">The turn turnContext</param>
        /// <param name="cancellationToken">The cancellation token</param>
        public async Task SignOutUserAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await ResetStateAsync(turnContext, cancellationToken).ConfigureAwait(false);
            await UserTokenClientWrapper.SignOutUserAsync(turnContext, _settings.ConnectionName, cancellationToken);
        }

        /// <summary>
        /// Get user token
        /// </summary>
        protected virtual async Task<TokenResponse> GetUserToken(ITurnContext turnContext, string connectionName, CancellationToken cancellationToken)
        {
            return await UserTokenClientWrapper.GetUserTokenAsync(turnContext, connectionName, "", cancellationToken).ConfigureAwait(false);
        }
    }
}
