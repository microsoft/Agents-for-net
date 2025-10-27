// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.UserAuth.AgenticAuth
{
    /// <summary>
    /// Handles OAuth using the Azure Bot Token Service.
    /// </summary>
    public class AgenticUserAuthorization : IUserAuthorization
    {
        private readonly IConnections _connections;
        private readonly AgenticAuthSettings _a365AuthSettings;

        /// <summary>
        /// Required constructor for type loader construction.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="storage"></param>
        /// <param name="connections"></param>
        /// <param name="configurationSection"></param>
        public AgenticUserAuthorization(string name, IStorage storage, IConnections connections, IConfigurationSection configurationSection)
            : this(name, storage, connections, configurationSection.Get<AgenticAuthSettings>())
        {
        }

        /// <summary>
        /// Initializes the class
        /// </summary>
        /// <param name="name">The authentication name.</param>
        /// <param name="settings">The settings to initialize the class</param>
        /// <param name="storage">The storage to use.</param>
        /// <param name="connections"></param>
        public AgenticUserAuthorization(string name, IStorage storage, IConnections connections, AgenticAuthSettings settings) 
        {
            AssertionHelpers.ThrowIfNull(connections, nameof(connections));

            _connections = connections;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _a365AuthSettings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public string Name { get; private set; }

        /// <inheritdoc/>
        public Task<TokenResponse> SignInUserAsync(ITurnContext turnContext, bool forceSignIn = false, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            return GetRefreshedUserTokenAsync(turnContext, exchangeConnection, exchangeScopes, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<TokenResponse> GetRefreshedUserTokenAsync(ITurnContext turnContext, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            if (!turnContext.Activity.IsAgenticRequest())
            {
                throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.NotAnAgenticRequest, null, "GetAgenticUserToken");
            }

            IAccessTokenProvider connection;
            if (!string.IsNullOrEmpty(_a365AuthSettings.AlternateBlueprintConnectionName))
            {
                connection = _connections.GetConnection(_a365AuthSettings.AlternateBlueprintConnectionName);
            }
            else
            {
                connection = _connections.GetTokenProvider(turnContext.Identity, turnContext.Activity);
            }

            if (connection is not IAgenticTokenProvider agenticTokenProvider)
            {
                throw ExceptionHelper.GenerateException<InvalidOperationException>(
                    ErrorHelper.AgenticTokenProviderNotFound, null, $"{AgentClaims.GetAppId(turnContext.Identity)}:{turnContext.Activity.ServiceUrl}");
            }

            var token = await agenticTokenProvider.GetAgenticUserTokenAsync(
                App.AgenticAuthorization.GetAgentInstanceId(turnContext),
                App.AgenticAuthorization.GetAgenticUser(turnContext),
                exchangeScopes ?? _a365AuthSettings.Scopes,
                cancellationToken).ConfigureAwait(false);

            return new TokenResponse(token: token);
        }

        /// <inheritdoc/>
        public Task ResetStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task SignOutUserAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
