// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.UserAuth.TokenService
{
    /// <summary>
    /// Handles OAuth using the Azure Bot Token Service.
    /// </summary>
    public class AzureBotUserAuthorization : OBOExchange, IUserAuthorization
    {
        private readonly OAuthSettings _settings;
        private readonly AgentUserAuthorization _agentAuthentication;

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
            : base(connections)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _agentAuthentication = new AgentUserAuthorization(name, settings, storage);
        }

        public string Name { get; private set; }

        protected override OBOSettings GetOBOSettings()
        {
            return _settings;
        }

        /// <inheritdoc/>
        public async Task<TokenResponse> SignInUserAsync(ITurnContext turnContext, bool forceSignIn = false, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            if (_agentAuthentication != null && (forceSignIn || await _agentAuthentication.IsValidActivity(turnContext, cancellationToken).ConfigureAwait(false)))
            {
                var token = await _agentAuthentication.OnFlowTurn(turnContext, cancellationToken).ConfigureAwait(false);

                try
                {
                    return await HandleOBO(turnContext, token, exchangeConnection, exchangeScopes, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    await SignOutUserAsync(turnContext, cancellationToken).ConfigureAwait(false);
                    throw;
                }
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
