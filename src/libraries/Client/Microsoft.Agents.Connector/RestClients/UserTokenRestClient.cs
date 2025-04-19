// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Connector.RestClients
{
    internal class UserTokenRestClient : IUserToken
    {
        private readonly IRestTransport _transport;
        private readonly static MemoryCache _cache = new MemoryCache(nameof(UserTokenRestClient));

        public UserTokenRestClient(IRestTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(_transport));
        }

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
        public async Task<object> ExchangeAsync(string userId, string connectionName, string channelId, TokenExchangeRequest exchangeRequest, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(userId);
            ArgumentException.ThrowIfNullOrEmpty(connectionName);
            ArgumentException.ThrowIfNullOrEmpty(channelId);
            ArgumentNullException.ThrowIfNull(exchangeRequest);

            using var message = CreateExchangeRequest(userId, connectionName, channelId, exchangeRequest);
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    return ProtocolJsonSerializer.ToObject<TokenResponse>(httpResponse.Content.ReadAsStream(cancellationToken));

                case 400:
                    var errorJson = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    return ProtocolJsonSerializer.ToObject<ErrorResponse>(errorJson);

                case 404:
                    var json = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    if (string.IsNullOrEmpty(json))
                    {
                        return null;
                    }
                    return ProtocolJsonSerializer.ToObject<TokenResponse>(httpResponse.Content.ReadAsStream(cancellationToken));

                default:
                    throw new HttpRequestException($"ExchangeAsyncAsync {httpResponse.StatusCode}");
            }
        }

        private static string CacheKey(string userId, string connectionName, string channelId)
        {
            return $"{userId}-{connectionName}-{channelId}";
        }

        /// <inheritdoc/>
        public async Task<TokenResponse> GetTokenAsync(string userId, string connectionName, string channelId = null, string code = null, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(userId);
            ArgumentException.ThrowIfNullOrEmpty(connectionName);

            var cacheKey = CacheKey(userId, connectionName, channelId);
            if (string.IsNullOrEmpty(code))
            {
                var value = _cache.Get(cacheKey);
                if (value != null)
                {
                    var toExpiration = ((TokenResponse)value).Expiration - DateTimeOffset.UtcNow;
                    if (toExpiration?.TotalMinutes >= 3)
                    {
                        return (TokenResponse)value;
                    }

                    _cache.Remove(cacheKey);
                }
            }

            using var message = CreateGetTokenRequest(userId, connectionName, channelId, code);
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    var json = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
                    var response = ProtocolJsonSerializer.ToObject<TokenResponse>(json);

                    if (response?.Token != null)
                    {
                        // Token Service isn't returning Expiration in TokenResponse
                        if (response.Expiration == null)
                        {
                            var jwtToken = new JwtSecurityToken(response.Token);
                            response.Expiration = jwtToken.ValidTo;
                        }

                        _cache.Add(
                            new CacheItem(cacheKey) { Value = response },
                            new CacheItemPolicy()
                            {
                                SlidingExpiration = TimeSpan.FromMinutes(3)
                            });
                    }

                    return response;

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
            ArgumentException.ThrowIfNullOrEmpty(userId);
            ArgumentException.ThrowIfNullOrEmpty(connectionName);

            using var message = CreateGetAadTokensRequest(userId, connectionName, channelId, aadResourceUrls);
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    {
                        return ProtocolJsonSerializer.ToObject<IReadOnlyDictionary<string, TokenResponse>>(httpResponse.Content.ReadAsStream(cancellationToken));
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
        public async Task<object> SignOutAsync(string userId, string connectionName, string channelId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(userId);

            _cache.Remove(CacheKey(userId, connectionName, channelId));

            using var message = CreateSignOutRequest(userId, connectionName, channelId);
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    {
                        return ProtocolJsonSerializer.ToObject<object>(httpResponse.Content.ReadAsStream(cancellationToken));
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
            ArgumentException.ThrowIfNullOrEmpty(userId);

            using var message = CreateGetTokenStatusRequest(userId, channelId, include);
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    {
                        return ProtocolJsonSerializer.ToObject<IReadOnlyList<TokenStatus>>(httpResponse.Content.ReadAsStream(cancellationToken));
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
            ArgumentException.ThrowIfNullOrEmpty(userId);
            ArgumentException.ThrowIfNullOrEmpty(connectionName);
            ArgumentException.ThrowIfNullOrEmpty(channelId);

            using var message = CreateExchangeTokenRequest(userId, connectionName, channelId, body);
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                case 404:
                    {
                        return ProtocolJsonSerializer.ToObject<TokenResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
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
            ArgumentException.ThrowIfNullOrEmpty(userId);
            ArgumentException.ThrowIfNullOrEmpty(connectionName);
            ArgumentException.ThrowIfNullOrEmpty(channelId);
            ArgumentException.ThrowIfNullOrEmpty(state);

            using var message = CreateGetTokenOrSignInResourceRequest(userId, connectionName, channelId, code, state, finalRedirect, fwdUrl);
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                case 404:
                    var json = await httpResponse.Content.ReadAsStringAsync();
                    return ProtocolJsonSerializer.ToObject<TokenOrSignInResourceResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
                default:
                    throw new HttpRequestException($"GetTokenOrSignInResourceAsync {httpResponse.StatusCode}");
            }
        }
    }
}
