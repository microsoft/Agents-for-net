// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Connector.RestClients
{
    internal class UserTokenRestClient(IRestTransport transport) : IUserToken
    {
        private readonly IRestTransport _transport = transport ?? throw new ArgumentNullException(nameof(_transport));

        internal HttpRequestMessage CreateGetTokenRequest(string userId, string connectionName, string channelId, string code)
        {
            var request = new HttpRequestMessage();
            request.Method = HttpMethod.Get;

            request.RequestUri = new Uri(_transport.Endpoint, "api/usertoken/GetToken")
                .AppendQuery("userId", userId)
                .AppendQuery("connectionName", connectionName)
                .AppendQuery("channelId", channelId)
                .AppendQuery("code", code);

            request.Headers.Add("Accept", "application/json");
            return request;
        }

        internal HttpRequestMessage CreateExchangeRequest(string userId, string connectionName, string channelId, TokenExchangeRequest body)
        {
            var request = new HttpRequestMessage();
            request.Method = HttpMethod.Post;

            request.RequestUri = new Uri(_transport.Endpoint, "api/usertoken/exchange")
                .AppendQuery("userId", userId)
                .AppendQuery("connectionName", connectionName)
                .AppendQuery("channelId", channelId);

            request.Headers.Add("Accept", "application/json");
            if (body != null)
            {
                request.Content = new StringContent(ProtocolJsonSerializer.ToJson(body), System.Text.Encoding.UTF8, "application/json");
            }
            return request;
        }

        /// <inheritdoc/>
        public async Task<object> ExchangeAsyncAsync(string userId, string connectionName, string channelId, TokenExchangeRequest exchangeRequest, CancellationToken cancellationToken = default)
        {
#if !NETSTANDARD
            ArgumentException.ThrowIfNullOrEmpty(userId);
            ArgumentException.ThrowIfNullOrEmpty(connectionName);
            ArgumentException.ThrowIfNullOrEmpty(channelId);
            ArgumentNullException.ThrowIfNull(exchangeRequest);
#else
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("UserId cannot be null or empty.", nameof(userId));
            }
            if (string.IsNullOrEmpty(connectionName))
            {
                throw new ArgumentException("ConnectionName cannot be null or empty.", nameof(connectionName));
            }
            if (string.IsNullOrEmpty(channelId))
            {
                throw new ArgumentException("ChannelId cannot be null or empty.", nameof(channelId));
            }
            if (exchangeRequest == null)
            {
                throw new ArgumentNullException(nameof(exchangeRequest));
            }
#endif

            using var message = CreateExchangeRequest(userId, connectionName, channelId, exchangeRequest);
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
#if !NETSTANDARD
                    return ProtocolJsonSerializer.ToObject<TokenResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
#else
                    var json = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (string.IsNullOrEmpty(json))
                    {
                        return null;
                    }
                    return ProtocolJsonSerializer.ToObject<TokenResponse>(json);
#endif

                case 400:
#if !NETSTANDARD
                    var errorJson = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    return ProtocolJsonSerializer.ToObject<ErrorResponse>(errorJson);
#else
                    var errorJson = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (string.IsNullOrEmpty(errorJson))
                    {
                        return null;
                    }
                    return ProtocolJsonSerializer.ToObject<ErrorResponse>(errorJson);
#endif

                case 404:
#if !NETSTANDARD
                    var json = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    if (string.IsNullOrEmpty(json))
                    {
                        return null;
                    }
                    return ProtocolJsonSerializer.ToObject<TokenResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
#else
                    var json1 = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (string.IsNullOrEmpty(json1))
                    {
                        return null;
                    }
                    return ProtocolJsonSerializer.ToObject<TokenResponse>(json1);
#endif

                default:
                    throw new HttpRequestException($"ExchangeAsyncAsync {httpResponse.StatusCode}");
            }
        }

        /// <inheritdoc/>
        public async Task<TokenResponse> GetTokenAsync(string userId, string connectionName, string channelId = null, string code = null, CancellationToken cancellationToken = default)
        {
#if !NETSTANDARD
            ArgumentException.ThrowIfNullOrEmpty(userId);
            ArgumentException.ThrowIfNullOrEmpty(connectionName);
#else
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("UserId cannot be null or empty.", nameof(userId));
            }
            if (string.IsNullOrEmpty(connectionName))
            {
                throw new ArgumentException("ConnectionName cannot be null or empty.", nameof(connectionName));
            }
#endif

            using var message = CreateGetTokenRequest(userId, connectionName, channelId, code);
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
#if !NETSTANDARD
                    return ProtocolJsonSerializer.ToObject<TokenResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
#else
                    var json = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (string.IsNullOrEmpty(json))
                    {
                        return null;
                    }
                    return ProtocolJsonSerializer.ToObject<TokenResponse>(json);
#endif
                case 404:
                    // there isn't a body provided in this case.  This can happen when the code is invalid.
                    return null;
                default:
                    throw new HttpRequestException($"GetTokenAsync {httpResponse.StatusCode}");
            }
        }

        internal HttpRequestMessage CreateGetAadTokensRequest(string userId, string connectionName, string channelId, AadResourceUrls body)
        {
            var request = new HttpRequestMessage();
            request.Method = HttpMethod.Post;

            request.RequestUri = new Uri(_transport.Endpoint, "api/usertoken/GetAadTokens")
                .AppendQuery("userId", userId)
                .AppendQuery("connectionName", connectionName)
                .AppendQuery("channelId", channelId);

            request.Headers.Add("Accept", "application/json");
            if (body != null)
            {
                request.Content = new StringContent(ProtocolJsonSerializer.ToJson(body), System.Text.Encoding.UTF8, "application/json");
            }
            return request;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyDictionary<string, TokenResponse>> GetAadTokensAsync(string userId, string connectionName, AadResourceUrls aadResourceUrls, string channelId = null, CancellationToken cancellationToken = default)
        {
#if !NETSTANDARD
            ArgumentException.ThrowIfNullOrEmpty(userId);
            ArgumentException.ThrowIfNullOrEmpty(connectionName);
#else
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("UserId cannot be null or empty.", nameof(userId));
            }
            if (string.IsNullOrEmpty(connectionName))
            {
                throw new ArgumentException("ConnectionName cannot be null or empty.", nameof(connectionName));
            }
#endif

            using var message = CreateGetAadTokensRequest(userId, connectionName, channelId, aadResourceUrls);
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    {
#if !NETSTANDARD
                        return ProtocolJsonSerializer.ToObject<IReadOnlyDictionary<string, TokenResponse>>(httpResponse.Content.ReadAsStream(cancellationToken));
#else
                        var json = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (string.IsNullOrEmpty(json))
                        {
                            return null;
                        }
                        return ProtocolJsonSerializer.ToObject<IReadOnlyDictionary<string, TokenResponse>>(json);
#endif
                    }
                default:
                    throw new HttpRequestException($"GetAadTokensAsync {httpResponse.StatusCode}");
            }
        }

        internal HttpRequestMessage CreateSignOutRequest(string userId, string connectionName, string channelId)
        {
            var request = new HttpRequestMessage();
            request.Method = HttpMethod.Delete;

            request.RequestUri = new Uri(_transport.Endpoint, "api/usertoken/SignOut")
                .AppendQuery("userId", userId)
                .AppendQuery("connectionName", connectionName)
                .AppendQuery("channelId", channelId);

            request.Headers.Add("Accept", "application/json");
            return request;
        }

        /// <inheritdoc/>
        public async Task<object> SignOutAsync(string userId, string connectionName = null, string channelId = null, CancellationToken cancellationToken = default)
        {
#if !NETSTANDARD
            ArgumentException.ThrowIfNullOrEmpty(userId);
#else
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("UserId cannot be null or empty.", nameof(userId));
            }
#endif

            using var message = CreateSignOutRequest(userId, connectionName, channelId);
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    {
#if !NETSTANDARD
                        return ProtocolJsonSerializer.ToObject<object>(httpResponse.Content.ReadAsStream(cancellationToken));
#else
                        var json = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (string.IsNullOrEmpty(json))
                        {
                            return null;
                        }
                        return ProtocolJsonSerializer.ToObject<object>(json);
#endif
                    }
                case 204:
                    return null;
                default:
                    throw new HttpRequestException($"SignOutAsync {httpResponse.StatusCode}");
            }
        }

        internal HttpRequestMessage CreateGetTokenStatusRequest(string userId, string channelId, string include)
        {
            var request = new HttpRequestMessage();
            request.Method = HttpMethod.Get;

            request.RequestUri = new Uri(_transport.Endpoint, "api/usertoken/GetTokenStatus")
                .AppendQuery("userId", userId)
                .AppendQuery("channelId", channelId)
                .AppendQuery("include", include);

            request.Headers.Add("Accept", "application/json");
            return request;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<TokenStatus>> GetTokenStatusAsync(string userId, string channelId = null, string include = null, CancellationToken cancellationToken = default)
        {
#if !NETSTANDARD
            ArgumentException.ThrowIfNullOrEmpty(userId);
#else
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("UserId cannot be null or empty.", nameof(userId));
            }
#endif

            using var message = CreateGetTokenStatusRequest(userId, channelId, include);
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    {
#if !NETSTANDARD
                        return ProtocolJsonSerializer.ToObject<IReadOnlyList<TokenStatus>>(httpResponse.Content.ReadAsStream(cancellationToken));
#else
                        var json = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (string.IsNullOrEmpty(json))
                        {
                            return null;
                        }
                        return ProtocolJsonSerializer.ToObject<IReadOnlyList<TokenStatus>>(json);
#endif
                    }
                default:
                    throw new HttpRequestException($"GetTokenStatusAsync {httpResponse.StatusCode}");
            }
        }

        internal HttpRequestMessage CreateExchangeTokenRequest(string userId, string connectionName, string channelId, TokenExchangeRequest body)
        {
            var request = new HttpRequestMessage();
            request.Method = HttpMethod.Post;

            request.RequestUri = new Uri(_transport.Endpoint, "api/usertoken/exchange")
                .AppendQuery("userId", userId)
                .AppendQuery("connectionName", connectionName)
                .AppendQuery("channelId", channelId);

            request.Headers.Add("Accept", "application/json");
            if (body != null)
            {
                request.Content = new StringContent(ProtocolJsonSerializer.ToJson(body), System.Text.Encoding.UTF8, "application/json");
            }
            return request;
        }

        /// <inheritdoc/>
        public async Task<TokenResponse> ExchangeTokenAsync(string userId, string connectionName, string channelId, TokenExchangeRequest body = null, CancellationToken cancellationToken = default)
        {
#if !NETSTANDARD

            ArgumentException.ThrowIfNullOrEmpty(userId);
            ArgumentException.ThrowIfNullOrEmpty(connectionName);
            ArgumentException.ThrowIfNullOrEmpty(channelId);
#else
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("UserId cannot be null or empty.", nameof(userId));
            }
            if (string.IsNullOrEmpty(connectionName))
            {
                throw new ArgumentException("ConnectionName cannot be null or empty.", nameof(connectionName));
            }
            if (string.IsNullOrEmpty(channelId))
            {
                throw new ArgumentException("ChannelId cannot be null or empty.", nameof(channelId));
            }
#endif

            using var message = CreateExchangeTokenRequest(userId, connectionName, channelId, body);
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                case 404:
                    {
#if !NETSTANDARD
                        return ProtocolJsonSerializer.ToObject<TokenResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
#else
                        var json = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (string.IsNullOrEmpty(json))
                        {
                            return null;
                        }
                        return ProtocolJsonSerializer.ToObject<TokenResponse>(json);
#endif
                    }
                default:
                    throw new HttpRequestException($"ExchangeTokenAsync {httpResponse.StatusCode}");
            }
        }

        internal HttpRequestMessage CreateGetTokenOrSignInResourceRequest(string userId, string connectionName, string channelId, string code, string state, string finalRedirect, string fwdUrl)
        {
            var request = new HttpRequestMessage();
            request.Method = HttpMethod.Get;

            request.RequestUri = new Uri(_transport.Endpoint, "api/usertoken/GetTokenOrSignInResource")
                .AppendQuery("userId", userId)
                .AppendQuery("connectionName", connectionName)
                .AppendQuery("channelId", channelId)
                .AppendQuery("code", code)
                .AppendQuery("state", state)
                .AppendQuery("finalRedirect", finalRedirect)
                .AppendQuery("fwdUrl", fwdUrl);

            request.Headers.Add("Accept", "application/json");
            return request;
        }

        /// <inheritdoc/>
        public async Task<TokenOrSignInResourceResponse> GetTokenOrSignInResourceAsync(string userId, string connectionName, string channelId, string state, string code = default, string finalRedirect = default, string fwdUrl = default, CancellationToken cancellationToken = default)
        {
#if !NETSTANDARD
            ArgumentException.ThrowIfNullOrEmpty(userId);
            ArgumentException.ThrowIfNullOrEmpty(connectionName);
            ArgumentException.ThrowIfNullOrEmpty(channelId);
            ArgumentException.ThrowIfNullOrEmpty(state);
#else
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("UserId cannot be null or empty.", nameof(userId));
            }
            if (string.IsNullOrEmpty(connectionName))
            {
                throw new ArgumentException("ConnectionName cannot be null or empty.", nameof(connectionName));
            }
            if (string.IsNullOrEmpty(channelId))
            {
                throw new ArgumentException("ChannelId cannot be null or empty.", nameof(channelId));
            }
            if (string.IsNullOrEmpty(state))
            {
                throw new ArgumentException("State cannot be null or empty.", nameof(state));
            }
#endif

            using var message = CreateGetTokenOrSignInResourceRequest(userId, connectionName, channelId, code, state, finalRedirect, fwdUrl);
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                case 404:
                    var json = await httpResponse.Content.ReadAsStringAsync();
#if !NETSTANDARD
                    return ProtocolJsonSerializer.ToObject<TokenOrSignInResourceResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
#else
                    if (string.IsNullOrEmpty(json))
                    {
                        return null;
                    }
                    return ProtocolJsonSerializer.ToObject<TokenOrSignInResourceResponse>(json);
#endif
                default:
                    throw new HttpRequestException($"GetTokenOrSignInResourceAsync {httpResponse.StatusCode}");
            }
        }
    }
}
