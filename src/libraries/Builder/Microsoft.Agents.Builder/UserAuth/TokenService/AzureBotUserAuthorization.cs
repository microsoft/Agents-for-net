// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.UserAuth.TokenService
{
    /// <summary>
    /// Handles OAuth using the Azure Bot Token Service.
    /// </summary>
    public class AzureBotUserAuthorization : IUserAuthorization
    {
        private readonly OAuthSettings _settings;
        private readonly AgentUserAuthorization _agentAuthentication;
        private readonly IConnections _connections;

        public AzureBotUserAuthorization(string name, IStorage storage, IConnections connections, IConfigurationSection configurationSection)
            : this(name, storage, connections, GetOAuthSettings(configurationSection))
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
            : this(settings, new AgentUserAuthorization(name, settings, storage))
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        }

        /// <summary>
        /// Initializes the class
        /// </summary>
        /// <param name="settings">The settings to initialize the class</param>
        /// <param name="agentAuthentication">The IUserAuthorizationFlow for the Agent.</param>
        internal AzureBotUserAuthorization(OAuthSettings settings, AgentUserAuthorization agentAuthentication)
        {
            _settings = settings;
            _agentAuthentication = agentAuthentication;
        }

        public string Name { get; private set; }

        /// <inheritdoc/>
        public async Task<TokenResponse> SignInUserAsync(ITurnContext turnContext, bool forceSignIn = false, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            if (_agentAuthentication != null && (forceSignIn || await _agentAuthentication.IsValidActivity(turnContext, cancellationToken).ConfigureAwait(false)))
            {
                var token = await _agentAuthentication.OnFlowTurn(turnContext, cancellationToken).ConfigureAwait(false);
                return await HandleOBO(turnContext, token, exchangeConnection, exchangeScopes, cancellationToken).ConfigureAwait(false);
            }

            return null;
        }

        /// <inheritdoc/>
        public async Task ResetStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await _agentAuthentication.ResetStateAsync(turnContext, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task SignOutUserAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await ResetStateAsync(turnContext, cancellationToken).ConfigureAwait(false);
            await UserTokenClientWrapper.SignOutUserAsync(turnContext, _settings.AzureBotOAuthConnectionName, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<TokenResponse> GetRefreshedUserTokenAsync(ITurnContext turnContext, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            var response = await UserTokenClientWrapper.GetUserTokenAsync(turnContext, _settings.AzureBotOAuthConnectionName, null, cancellationToken).ConfigureAwait(false);
            return await HandleOBO(turnContext, response, exchangeConnection, exchangeScopes, cancellationToken).ConfigureAwait(false);
        }

        private async Task<TokenResponse> HandleOBO(ITurnContext turnContext, TokenResponse token, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(token?.Token))
            {
                return null;
            }

            var connectionName = exchangeConnection ?? _settings.OBOConnectionName;
            var scopes = exchangeScopes ?? _settings.OBOScopes;

            // If OBO is not supplied (by config or passed) return token as-is.
            if (string.IsNullOrEmpty(connectionName) || scopes == null || !scopes.Any())
            {
                return token;
            }

            try
            {
                // Can we even exchange this?
                if (!token.IsExchangeable)
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
                    token = await oboExchangeProvider.AcquireTokenOnBehalfOf(scopes, token.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.OBOExchangeFailed, ex, [connectionName, string.Join(",", scopes)]);
                }

                if (token == null)
                {
                    // AcquireTokenOnBehalfOf returned null
                    throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.OBOExchangeFailed, null, [connectionName, string.Join(",", scopes)]);
                }
            }
            catch (Exception)
            {
                await SignOutUserAsync(turnContext, cancellationToken).ConfigureAwait(false);
                throw;
            }

            return token ?? null;
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

        private static OAuthSettings GetOAuthSettings(IConfigurationSection config)
        {
            var settings = config.Get<OAuthSettings>();

            if (settings.OBOScopes == null)
            {
                // try reading as a string to compensate for users just setting a non-array string
                var configScope = config.GetSection(nameof(OAuthSettings.OBOScopes)).Get<string>();
                if (!string.IsNullOrEmpty(configScope))
                {
                    settings.OBOScopes = [configScope];
                }
            }

            return settings;
        }
    }
}
