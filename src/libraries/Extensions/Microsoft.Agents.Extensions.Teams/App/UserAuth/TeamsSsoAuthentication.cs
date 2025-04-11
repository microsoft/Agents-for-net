// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Extensions.Teams.App.UserAuth
{
    /// <summary>
    /// Handles authentication based on Teams SSO.
    /// </summary>
    public class TeamsSsoAuthentication : IUserAuthorization
    {
        private IConfidentialClientApplicationAdapter _msalAdapter;
        private readonly TeamsSsoBotAuthentication _botAuth;
        //private TeamsSsoMessageExtensionsAuthentication? _messageExtensionsAuth;
        private readonly TeamsSsoSettings _settings;
        private readonly string _name;

        /// <summary>
        /// Initialize instance for current class
        /// </summary>
        /// <param name="app">The application.</param>
        /// <param name="name">The authentication name.</param>
        /// <param name="settings">The settings to initialize the class</param>
        /// <param name="storage">The storage to use.</param>
        public TeamsSsoAuthentication(string name, TeamsSsoSettings settings, IStorage storage)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _settings = settings;
            _msalAdapter = new ConfidentialClientApplicationAdapter(settings.MSAL);
            Name = name ?? throw new ArgumentNullException(nameof(name));

            _botAuth = new TeamsSsoBotAuthentication(name, _settings, storage, _msalAdapter);
            //_messageExtensionsAuth = new TeamsSsoMessageExtensionsAuthentication(_settings);
        }

        public string Name { get; private set; }

        /// <summary>
        /// Sign in current user
        /// </summary>
        /// <param name="context">The turn context</param>
        /// <param name="forceSignIn"></param>
        /// <param name="exchangeConnection"></param>
        /// <param name="exchangeScopes"></param>
        /// <param name="state">The turn state</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The sign in response</returns>
        public async Task<string> SignInUserAsync(ITurnContext context, bool forceSignIn = false, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            var token = await _msalAdapter.TryGetUserToken(context, _name, _settings).ConfigureAwait(false);
            if (token != null)
            {
                return token.Token;
            }

            if ((_botAuth != null && _botAuth.IsValidActivity(context)))
            {
                return await _botAuth.AuthenticateAsync(context, cancellationToken).ConfigureAwait(false);
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
            var token = await _msalAdapter.TryGetUserToken(context, _name, _settings).ConfigureAwait(false);
            return token?.Token;
        }

        public async Task ResetStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await _botAuth.ResetStateAsync(turnContext, cancellationToken).ConfigureAwait(false);
        }
    }
}
