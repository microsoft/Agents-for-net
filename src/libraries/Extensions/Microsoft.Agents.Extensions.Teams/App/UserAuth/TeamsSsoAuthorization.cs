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

namespace Microsoft.Agents.Extensions.Teams.App.UserAuth
{
    /// <summary>
    /// Handles authentication based on Teams SSO.
    /// </summary>
    public class TeamsSsoAuthorization : IUserAuthorization
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
        public TeamsSsoAuthorization(string name, IStorage storage, IConnections connections, TeamsSsoSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _msalAdapter = GetMsalAdapter(connections);

            _botAuth = new TeamsSsoBotAuthorization(name, _settings, storage, _msalAdapter);
            //_messageExtensionsAuth = new TeamsSsoMessageExtensionsAuthentication(_settings);
        }

        public string Name { get; private set; }

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
        public async Task<string> SignInUserAsync(ITurnContext turnContext, bool forceSignIn = false, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            if (!turnContext.Activity.ChannelId.Equals(Channels.Msteams, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new UnsupportedChannel();
            }

            var token = await _msalAdapter.TryGetUserToken(turnContext, Name, _settings).ConfigureAwait(false);
            if (token != null)
            {
                return token.Token;
            }

            if (_botAuth != null && (forceSignIn || _botAuth.IsValidActivity(turnContext)))
            {
                return await _botAuth.AuthenticateAsync(turnContext, cancellationToken).ConfigureAwait(false);
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
        public async Task SignOutUserAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            string homeAccountId = $"{turnContext.Activity.From.AadObjectId}.{turnContext.Activity.Conversation.TenantId}";

            await _msalAdapter.StopLongRunningProcessInWebApiAsync(homeAccountId, cancellationToken);
        }

        /// <summary>
        /// Check if the user is signed, if they are then return the token.
        /// </summary>
        /// <param name="context">The turn context.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The token if the user is signed. Otherwise null.</returns>
        public async Task<string> IsUserSignedInAsync(ITurnContext context, CancellationToken cancellationToken = default)
        {
            var token = await _msalAdapter.TryGetUserToken(context, Name, _settings).ConfigureAwait(false);
            return token?.Token;
        }

        public async Task ResetStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await _botAuth.ResetStateAsync(turnContext, cancellationToken).ConfigureAwait(false);
        }

        private ConfidentialClientApplicationAdapter GetMsalAdapter(IConnections connections)
        {
            var client = connections.GetConnection(_settings.ConnectionName);
            if (client is IMSALProvider msal)
            {
                return new ConfidentialClientApplicationAdapter((IConfidentialClientApplication) msal.CreateClientApplication());
            }

            throw new InvalidOperationException($"Connection '{_settings.ConnectionName}' does not support MSAL");
        }
    }
}
