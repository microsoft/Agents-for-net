// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.BotBuilder.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.BotBuilder.UserAuth.TokenService
{
    /// <summary>
    /// Handles user authentication using The Azure Bot Token Service.
    /// </summary>
    public class AzureBotUserAuthorization : IUserAuthorization
    {
        private readonly OAuthSettings _settings;
        //private readonly OAuthMessageExtensionsAuthentication? _messageExtensionAuth;
        private readonly BotUserAuthorization _botAuthentication;
        private readonly IConnections _connections;

        public AzureBotUserAuthorization(string name, IStorage storage, IConnections connections, IConfigurationSection configurationSection)
            : this(name, storage, connections, configurationSection.Get<OAuthSettings>())
        {

        }

        /// <summary>
        /// Initializes the class
        /// </summary>
        /// <param name="name">The authentication name.</param>
        /// <param name="settings">The settings to initialize the class</param>
        /// <param name="storage">The storage to use.</param>
        /// <param name="connections"></param>
        public AzureBotUserAuthorization(string name, IStorage storage, IConnections connections, OAuthSettings settings) 
            : this(settings, new BotUserAuthorization(name, settings, storage))
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        }

        /// <summary>
        /// Initializes the class
        /// </summary>
        /// <param name="settings">The settings to initialize the class</param>
        /// <param name="botAuthentication">The bot authentication instance</param>
        /// <param name="messageExtensionAuth">The message extension authentication instance</param>
        internal AzureBotUserAuthorization(OAuthSettings settings, BotUserAuthorization botAuthentication)
        {
            _settings = settings;
            //_messageExtensionAuth = messageExtensionAuth;
            _botAuthentication = botAuthentication;
        }

        public string Name { get; private set; }

        /// <inheritdoc/>
        public async Task<string> SignInUserAsync(ITurnContext turnContext, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            /*
            if ((_messageExtensionAuth != null && _messageExtensionAuth.IsValidActivity(turnContext)))
            {
                return await _messageExtensionAuth.AuthenticateAsync(turnContext).ConfigureAwait(false);
            }
            */

            if (_botAuthentication != null && _botAuthentication.IsValidActivity(turnContext))
            {
                var token = await _botAuthentication.AuthenticateAsync(turnContext, cancellationToken).ConfigureAwait(false);
                return await HandleOBO(turnContext, token, exchangeConnection, exchangeScopes, cancellationToken).ConfigureAwait(false);
            }

            return null;
        }

        /// <inheritdoc/>
        public async Task ResetStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await _botAuthentication.ResetStateAsync(turnContext, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task SignOutUserAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await ResetStateAsync(turnContext, cancellationToken).ConfigureAwait(false);
            await UserTokenClientWrapper.SignOutUserAsync(turnContext, _settings.AzureBotOAuthConnectionName, cancellationToken);
        }

        /// <summary>
        /// Get user token
        /// </summary>
        protected virtual async Task<TokenResponse> GetUserToken(ITurnContext turnContext, string connectionName, CancellationToken cancellationToken)
        {
            return await UserTokenClientWrapper.GetUserTokenAsync(turnContext, connectionName, "", cancellationToken).ConfigureAwait(false);
        }

        private async Task<string> HandleOBO(ITurnContext turnContext, string token, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(token))
            {
                return null;
            }

            // If OBO is not set return token as-is.
            if (string.IsNullOrEmpty(_settings.OBOConnectionName) && string.IsNullOrEmpty(exchangeConnection))
            {
                return token;
            }

            string exchangedToken = null;
            var connectionName = exchangeConnection ?? _settings.OBOConnectionName;

            try
            {
                // Can we even exchange this?
                if (!IsExchangeableToken(token))
                {
                    throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.OBONotExchangeableToken, null, [connectionName]);
                }

                // Can the named Connection even do this?
                if (!TryGetOBOProvider(connectionName, out var oboExchangeProvider))
                {
                    throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.OBONotSupported, null, [connectionName]);
                }

                // Do exchange.
                try
                {
                    exchangedToken = await oboExchangeProvider.AcquireTokenOnBehalfOf(exchangeScopes ?? _settings.OBOScopes, token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.OBOExchangeFailed, ex, [connectionName, $"[{(exchangeScopes == null ? "null" : string.Join(",", exchangeScopes))}]"]);
                }
            }
            finally
            {
                if (exchangedToken == null)
                {
                    await UserTokenClientWrapper.SignOutUserAsync(turnContext, _settings.AzureBotOAuthConnectionName, cancellationToken).ConfigureAwait(false);
                }
            }

            return exchangedToken;
        }

        private static bool IsExchangeableToken(string token)
        {
            JwtSecurityToken jwtToken = new(token);
            var aud = jwtToken.Claims.FirstOrDefault(claim => claim.Type == AuthenticationConstants.AudienceClaim)?.Value;
            return (bool)(aud?.StartsWith("api://"));
        }

        private bool TryGetOBOProvider(string connectionName, out IOBOExchange oboExchangeProvider)
        {
            if (_connections.TryGetConnection(connectionName, out var tokenProvider))
            {
                if (tokenProvider is IOBOExchange oboExchange)
                {
                    oboExchangeProvider = oboExchange;
                    return true;
                }
            }

            oboExchangeProvider = null;
            return false;
        }
    }
}
