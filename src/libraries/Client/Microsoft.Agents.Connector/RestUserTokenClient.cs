// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Connector.RestClients;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Agents.Core.Errors;
using System.Diagnostics;

namespace Microsoft.Agents.Connector
{
    /// <summary>
    /// Client for OAuth flow using the TokenAPI endpoints.  This uses the Azure Bot Service Token Service to facilitate the token exchange.
    /// </summary>
    public class RestUserTokenClient : RestClientBase, IUserTokenClient, IDisposable
    {
        private readonly string _appId;
        private readonly UserTokenRestClient _userTokenClient;
        private readonly AgentSignInRestClient _agentSignInClient;
        private readonly ILogger _logger;
        private bool _disposed;

        public Uri BaseUri => Endpoint;

        public RestUserTokenClient(string appId, Uri endpoint, IHttpClientFactory httpClientFactory, Func<Task<string>> tokenProviderFunction, string namedClient = nameof(RestUserTokenClient), ILogger logger = null)
            : base(endpoint, httpClientFactory, namedClient, tokenProviderFunction)
        {
#if !NETSTANDARD
            ArgumentException.ThrowIfNullOrEmpty(appId);
#else
            if (string.IsNullOrEmpty(appId))
            {
                throw new ArgumentException("AppId cannot be null or empty.", nameof(appId));
            }
#endif

            _appId = appId;

            _userTokenClient = new UserTokenRestClient(this);
            _agentSignInClient = new AgentSignInRestClient(this);
            _logger = logger ?? NullLogger.Instance;
        }

        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);

            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        /// <inheritdoc />
        public async Task<TokenResponse> GetUserTokenAsync(string userId, string connectionName, string channelId, string magicCode, CancellationToken cancellationToken)
        {
#if !NETSTANDARD
            ObjectDisposedException.ThrowIf(_disposed, nameof(GetUserTokenAsync));
            ArgumentException.ThrowIfNullOrEmpty(userId);
            ArgumentException.ThrowIfNullOrEmpty(connectionName);
#else
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GetUserTokenAsync));
            }
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("UserId cannot be null or empty.", nameof(userId));
            }
            if (string.IsNullOrEmpty(connectionName))
            {
                throw new ArgumentException("ConnectionName cannot be null or empty.", nameof(connectionName));
            }
#endif

            _logger.LogInformation($"GetTokenAsync ConnectionName: {connectionName}");
            return await _userTokenClient.GetTokenAsync(userId, connectionName, channelId, magicCode, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<SignInResource> GetSignInResourceAsync(string connectionName, IActivity activity, string finalRedirect, CancellationToken cancellationToken)
        {
#if !NETSTANDARD
            ObjectDisposedException.ThrowIf(_disposed, nameof(GetSignInResourceAsync));
            ArgumentException.ThrowIfNullOrEmpty(connectionName);
            ArgumentNullException.ThrowIfNull(activity);
#else
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GetSignInResourceAsync));
            }
            if (string.IsNullOrEmpty(connectionName))
            {
                throw new ArgumentException("ConnectionName cannot be null or empty.", nameof(connectionName));
            }
            if (activity == null)
            {
                throw new ArgumentNullException(nameof(activity));
            }
#endif

            _logger.LogInformation($"GetSignInResourceAsync ConnectionName: {connectionName}");
            var state = CreateTokenExchangeState(_appId, connectionName, activity);
            return await _agentSignInClient.GetSignInResourceAsync(state, null, null, finalRedirect, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task SignOutUserAsync(string userId, string connectionName, string channelId, CancellationToken cancellationToken)
        {
#if !NETSTANDARD
            ObjectDisposedException.ThrowIf(_disposed, nameof(SignOutUserAsync));
            ArgumentException.ThrowIfNullOrEmpty(userId);
            ArgumentException.ThrowIfNullOrEmpty(connectionName);
#else
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SignOutUserAsync));
            }
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("UserId cannot be null or empty.", nameof(userId));
            }
            if (string.IsNullOrEmpty(connectionName))
            {
                throw new ArgumentException("ConnectionName cannot be null or empty.", nameof(connectionName));
            }
#endif

            _logger.LogInformation($"SignOutAsync ConnectionName: {connectionName}");
            await _userTokenClient.SignOutAsync(userId, connectionName, channelId, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<TokenStatus[]> GetTokenStatusAsync(string userId, string channelId, string includeFilter, CancellationToken cancellationToken)
        {
#if !NETSTANDARD
            ObjectDisposedException.ThrowIf(_disposed, nameof(GetTokenStatusAsync));
            ArgumentException.ThrowIfNullOrEmpty(userId);
            ArgumentException.ThrowIfNullOrEmpty(channelId);
#else
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GetTokenStatusAsync));
            }
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("UserId cannot be null or empty.", nameof(userId));
            }
            if (string.IsNullOrEmpty(channelId))
            {
                throw new ArgumentException("ChannelId cannot be null or empty.", nameof(channelId));
            }
#endif

            _logger.LogInformation("GetTokenStatusAsync");
            var result = await _userTokenClient.GetTokenStatusAsync(userId, channelId, includeFilter, cancellationToken).ConfigureAwait(false);
            return result?.ToArray();
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, TokenResponse>> GetAadTokensAsync(string userId, string connectionName, string[] resourceUrls, string channelId, CancellationToken cancellationToken)
        {
#if !NETSTANDARD
            ObjectDisposedException.ThrowIf(_disposed, nameof(GetAadTokensAsync));
            ArgumentException.ThrowIfNullOrEmpty(userId);
            ArgumentException.ThrowIfNullOrEmpty(connectionName);
#else
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GetAadTokensAsync));
            }
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("UserId cannot be null or empty.", nameof(userId));
            }
            if (string.IsNullOrEmpty(connectionName))
            {
                throw new ArgumentException("ConnectionName cannot be null or empty.", nameof(connectionName));
            }
#endif

            _logger.LogInformation($"GetAadTokensAsync ConnectionName: {connectionName}");
            return (Dictionary<string, TokenResponse>)await _userTokenClient.GetAadTokensAsync(userId, connectionName, new AadResourceUrls() { ResourceUrls = resourceUrls?.ToList() }, channelId, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<TokenResponse> ExchangeTokenAsync(string userId, string connectionName, string channelId, TokenExchangeRequest exchangeRequest, CancellationToken cancellationToken)
        {
#if !NETSTANDARD
            ObjectDisposedException.ThrowIf(_disposed, nameof(ExchangeTokenAsync));
            ArgumentException.ThrowIfNullOrEmpty(userId);
            ArgumentException.ThrowIfNullOrEmpty(connectionName);
#else
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ExchangeTokenAsync));
            }
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("UserId cannot be null or empty.", nameof(userId));
            }
            if (string.IsNullOrEmpty(connectionName))
            {
                throw new ArgumentException("ConnectionName cannot be null or empty.", nameof(connectionName));
            }
#endif

            _logger.LogInformation($"ExchangeAsyncAsync ConnectionName: {connectionName}");
            var result = await _userTokenClient.ExchangeAsyncAsync(userId, connectionName, channelId, exchangeRequest, cancellationToken).ConfigureAwait(false);
            if (result == null)
            {
                return null;
            }

            if (result is ErrorResponse errorResponse)
            {
                throw new InvalidOperationException($"Unable to exchange token: ({errorResponse?.Error?.Code}) {errorResponse?.Error?.Message}");
            }
            else if (result is TokenResponse tokenResponse)
            {
                return tokenResponse;
            }
            else
            {
                throw new InvalidOperationException($"ExchangeAsyncAsync returned improper result: {result.GetType()}");
            }
        }

        /// <inheritdoc />
        public async Task<TokenOrSignInResourceResponse> GetTokenOrSignInResourceAsync(string connectionName, IActivity activity, string code, string finalRedirect = null, string fwdUrl = null, CancellationToken cancellationToken = default)
        {
#if !NETSTANDARD
            ObjectDisposedException.ThrowIf(_disposed, nameof(GetTokenOrSignInResourceAsync));
            ArgumentNullException.ThrowIfNull(activity);
            ArgumentException.ThrowIfNullOrEmpty(connectionName);
#else
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GetTokenOrSignInResourceAsync));
            }
            if (activity == null)
            {
                throw new ArgumentNullException(nameof(activity));
            }
            if (string.IsNullOrEmpty(connectionName))
            {
                throw new ArgumentException("ConnectionName cannot be null or empty.", nameof(connectionName));
            }
#endif
            _logger.LogInformation($"GetTokenOrSignInResourceAsync ConnectionName: {connectionName}");
            var state = CreateTokenExchangeState(_appId, connectionName, activity);
            return await _userTokenClient.GetTokenOrSignInResourceAsync(activity.From.Id, connectionName, activity.ChannelId, state, code, finalRedirect, fwdUrl, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Helper function to create the base64 encoded token exchange state used in GetSignInResourceAsync calls.
        /// </summary>
        /// <param name="appId">The appId to include in the token exchange state.</param>
        /// <param name="connectionName">The connectionName to include in the token exchange state.</param>
        /// <param name="activity">The <see cref="IActivity"/> from which to derive the token exchange state.</param>
        /// <returns>base64 encoded token exchange state.</returns>
        public static string CreateTokenExchangeState(string appId, string connectionName, IActivity activity)
        {
            var tokenExchangeState = new TokenExchangeState
            {
                ConnectionName = connectionName,
                Conversation = activity.GetConversationReference(),
                RelatesTo = activity.RelatesTo,
                MsAppId = appId,
            };
            var json = ProtocolJsonSerializer.ToJson(tokenExchangeState);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }
    }
}
