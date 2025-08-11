// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Storage;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using Microsoft.Agents.Authentication;
using Microsoft.Identity.Client;
using Microsoft.Agents.Authentication.Msal;
using Microsoft.Extensions.Configuration;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Builder.UserAuth.TokenService;

namespace Microsoft.Agents.Extensions.Teams.App.UserAuth
{
    /// <summary>
    /// Handles authentication based on Teams SSO.
    /// </summary>
    public class TeamsSsoAuthorization : AzureBotUserAuthorization, IUserAuthorization
    {
        private readonly ConfidentialClientApplicationAdapter _msalAdapter;
        private readonly TeamsSsoBotAuthorization _botAuth;
        //private TeamsSsoMessageExtensionsAuthentication? _messageExtensionsAuth;
        private readonly TeamsSsoSettings _settings;

        public TeamsSsoAuthorization(string name, IStorage storage, IConnections connections, IConfigurationSection configurationSection)
            : this(name, storage, connections, configurationSection.Get<TeamsSsoSettings>())
        {

        }

        /// <summary>
        /// Initialize instance for current class
        /// </summary>
        /// <param name="app">The application.</param>
        /// <param name="name">The authentication name.</param>
        /// <param name="settings">The settings to initialize the class</param>
        /// <param name="connections"></param>
        /// <param name="storage">The storage to use.</param>
        public TeamsSsoAuthorization(string name, IStorage storage, IConnections connections, TeamsSsoSettings settings) : base(name, storage, connections, settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _msalAdapter = GetMsalAdapter(connections);

            _botAuth = new TeamsSsoBotAuthorization(name, _settings, storage, _msalAdapter);
            //_messageExtensionsAuth = new TeamsSsoMessageExtensionsAuthentication(_settings);
        }

        /// <summary>
        /// Sign in current user
        /// </summary>
        /// <param name="turnContext">The turn context</param>
        /// <param name="forceSignIn"></param>
        /// <param name="exchangeConnection"></param>
        /// <param name="exchangeScopes"></param>
        /// <param name="state">The turn state</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The sign in response</returns>
        async Task<TokenResponse> IUserAuthorization.SignInUserAsync(ITurnContext turnContext, bool forceSignIn, string exchangeConnection, IList<string> exchangeScopes, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.ChannelId != Channels.Msteams)
            {
                return await base.SignInUserAsync(turnContext, forceSignIn, exchangeConnection, exchangeScopes, cancellationToken).ConfigureAwait(false);
            }

            var token = await _msalAdapter.TryGetUserToken(turnContext, Name, _settings).ConfigureAwait(false);
            if (token != null)
            {
                return new TokenResponse()
                {
                    Token = token.Token
                };
            }

            if (_botAuth != null && (forceSignIn || _botAuth.IsValidActivity(turnContext)))
            {
               return new TokenResponse() { Token = await _botAuth.AuthenticateAsync(turnContext, cancellationToken).ConfigureAwait(false) };
            }

            /*
            if ((_messageExtensionsAuth != null && _messageExtensionsAuth.IsValidActivity(context)))
            {
                return await _messageExtensionsAuth.AuthenticateAsync(context);
            }
            */

            return null;
        }

        /// <summary>
        /// Sign out current user
        /// </summary>
        /// <param name="turnContext">The turn context</param>
        /// <param name="cancellationToken">The cancellation token</param>
        async Task IUserAuthorization.SignOutUserAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.ChannelId != Channels.Msteams)
            {
                await base.SignOutUserAsync(turnContext, cancellationToken).ConfigureAwait(false);
                return;
            }

            string homeAccountId = $"{turnContext.Activity.From.AadObjectId}.{turnContext.Activity.Conversation.TenantId}";

            await _msalAdapter.StopLongRunningProcessInWebApiAsync(homeAccountId, cancellationToken);
        }

        async Task IUserAuthorization.ResetStateAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.ChannelId != Channels.Msteams)
            {
                await base.ResetStateAsync(turnContext, cancellationToken).ConfigureAwait(false);
                return;
            }

            await _botAuth.ResetStateAsync(turnContext, cancellationToken).ConfigureAwait(false);
        }

        private ConfidentialClientApplicationAdapter GetMsalAdapter(IConnections connections)
        {
            var client = connections.GetConnection(_settings.SsoConnectionName);
            if (client is IMSALProvider msal)
            {
                return new ConfidentialClientApplicationAdapter((IConfidentialClientApplication) msal.CreateClientApplication());
            }

            throw new InvalidOperationException($"Connection '{_settings.SsoConnectionName}' does not support MSAL");
        }
    }
}
