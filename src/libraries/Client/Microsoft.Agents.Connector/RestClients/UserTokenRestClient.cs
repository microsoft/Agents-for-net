// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Connector.Errors;
using Microsoft.Agents.Core;
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
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
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
        public async Task<TokenResponse> ExchangeAsync(string userId, string connectionName, string channelId, TokenExchangeRequest exchangeRequest, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(userId, nameof(userId));
            AssertionHelpers.ThrowIfNullOrEmpty(connectionName, nameof(connectionName));
            AssertionHelpers.ThrowIfNullOrEmpty(channelId, nameof(channelId));
            AssertionHelpers.ThrowIfNull(exchangeRequest, nameof(exchangeRequest));

            using var message = CreateExchangeRequest(userId, connectionName, channelId, exchangeRequest);
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
#if !NETSTANDARD
                    var tokenResponse = ProtocolJsonSerializer.ToObject<TokenResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
#else
                    var json = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (string.IsNullOrEmpty(json))
                    {
                        return null;
                    }
                    var tokenResponse = ProtocolJsonSerializer.ToObject<TokenResponse>(json);
#endif
                    if (tokenResponse?.Token != null)
                    {
                        AddTokenResponseToCache(CacheKey(userId, connectionName, channelId), tokenResponse);
                    }
                    return tokenResponse;

                // Consent Required
                case 400:
#if !NETSTANDARD
                    ErrorResponse errorBody = ProtocolJsonSerializer.ToObject<ErrorResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
#else
                    ErrorResponse errorBody = ProtocolJsonSerializer.ToObject<ErrorResponse>(httpResponse.Content.ReadAsStringAsync().Result);
#endif
                    errorBody.Error.Code = Error.ConsentRequiredCode;
                    throw new ErrorResponseException($"({errorBody.Error.Code}) {errorBody.Error.Message}") { Body = errorBody };

                // Unclear what this means
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

                // Normal when OAuth Connection config is wrong
                case 500:
                    throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.TokenServiceExchangeFailed, cancellationToken, connectionName);

                // Unknown
                default:
                    throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.TokenServiceExchangeUnexpected, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
            }
        }
        
        private static string CacheKey(string userId, string connectionName, string channelId)
        {
            return $"{userId}-{connectionName}-{channelId}";
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

        /// <inheritdoc/>
        public async Task<TokenResponse> GetTokenAsync(string userId, string connectionName, string channelId = null, string code = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(userId, nameof(userId));
            AssertionHelpers.ThrowIfNullOrEmpty(connectionName, nameof(connectionName));

            var cacheKey = CacheKey(userId, connectionName, channelId);
            if (string.IsNullOrEmpty(code))
            {
                var cachedTokenResponse = GetTokenResponseFromCache(cacheKey);
                if (cachedTokenResponse != null)
                {
                    return cachedTokenResponse;
                }
            }

            using var message = CreateGetTokenRequest(userId, connectionName, channelId, code);
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
#if !NETSTANDARD
                    var json = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
                    var json = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif                    
                    if (string.IsNullOrEmpty(json))
                    {
                        return null;
                    }
                    
                    var response = ProtocolJsonSerializer.ToObject<TokenResponse>(json);

                    AddTokenResponseToCache(cacheKey, response);

                    return response;

                case 404:
                    // there isn't a body provided in this case.  This can happen when the code is invalid.
                    return null;

                default:
                    throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.TokenServiceGetTokenUnexpected, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
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
            AssertionHelpers.ThrowIfNullOrEmpty(userId, nameof(userId));
            AssertionHelpers.ThrowIfNullOrEmpty(connectionName, nameof(connectionName));

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
                    throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.TokenServiceGetAadTokenUnexpected, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
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
            AssertionHelpers.ThrowIfNullOrEmpty(userId, nameof(userId));

            _cache.Remove(CacheKey(userId, connectionName, channelId));

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
                    throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.TokenServiceSignOutUnexpected, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
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
            AssertionHelpers.ThrowIfNullOrEmpty(userId, nameof(userId));

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
                    throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.TokenServiceGetTokenStatusUnexpected, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
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
            AssertionHelpers.ThrowIfNullOrEmpty(userId, nameof(userId));
            AssertionHelpers.ThrowIfNullOrEmpty(connectionName, nameof(connectionName));
            AssertionHelpers.ThrowIfNullOrEmpty(channelId, nameof(channelId));
            AssertionHelpers.ThrowIfNullOrEmpty(state, nameof(state));

            var cacheKey = CacheKey(userId, connectionName, channelId);
            if (string.IsNullOrEmpty(code))
            {
                var cachedTokenResponse = GetTokenResponseFromCache(cacheKey);
                if (cachedTokenResponse != null)
                {
                    return new TokenOrSignInResourceResponse() { TokenResponse = cachedTokenResponse };
                }
            }

            using var message = CreateGetTokenOrSignInResourceRequest(userId, connectionName, channelId, code, state, finalRedirect, fwdUrl);
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
#if !NETSTANDARD
                    var json = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
                    var json = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif                    
                    if (string.IsNullOrEmpty(json))
                    {
                        return null;
                    }

                    var response = ProtocolJsonSerializer.ToObject<TokenOrSignInResourceResponse>(json);

                    AddTokenResponseToCache(cacheKey, response.TokenResponse);

                    return response;

                case 404:
                    return null;

                default:
                    throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.TokenServiceGetTokenOrSignInResourceUnexpected, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
            }
        }

        private static TokenResponse GetTokenResponseFromCache(string cacheKey)
        {
            var value = _cache.Get(cacheKey);
            if (value != null)
            {
                // Token Service will renew within 5 minutes of expiration.  Return the cached token
                // if there is more than that. Otherwise, remove it from the cache and return null.  This
                // will result in a call to the Token Service to get a new token.
                var toExpiration = ((TokenResponse)value).Expiration - DateTimeOffset.UtcNow;
                if (toExpiration?.TotalMinutes >= 5)
                {
                    return (TokenResponse)value;
                }

                _cache.Remove(cacheKey);
            }

            return null;
        }

        private static void AddTokenResponseToCache(string cacheKey, TokenResponse tokenResponse)
        {
            if (tokenResponse != null && tokenResponse.Token != null)
            {
                try
                {
                    var jwtToken = new JwtSecurityToken(tokenResponse.Token);
                    if (tokenResponse.Expiration == null)
                    {
                        // It's usually the case that the TokenResponse will NOT include an expiration value,
                        // in which case we will use the JWT token expiration value.
                        tokenResponse.Expiration = jwtToken.ValidTo;
                    }
                    tokenResponse.IsExchangeable = IsExchangeableToken(jwtToken);
                }
                catch (Exception)
                {
                    tokenResponse.IsExchangeable = false;
                }

                // If the TokenResponse doesn't contain an expiration value then expiration calcs
                // won't be available to callers.  But the token can otherwise be used.  However,
                // we'll skip caching for now.
                if (tokenResponse.Expiration != null)
                {
                    _cache.Add(
                        new CacheItem(cacheKey) { Value = tokenResponse },
                        new CacheItemPolicy()
                        {
                            SlidingExpiration = TimeSpan.FromMinutes(5)
                        });
                }
            }
        }

        private static bool IsExchangeableToken(JwtSecurityToken jwtToken)
        {
            var aud = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "aud");
            return aud != null && aud.Value.StartsWith("api://");
        }
    }
}
