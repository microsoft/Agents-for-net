﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Core;
using Microsoft.Agents.Authentication.Msal.Model;
using Microsoft.Agents.Authentication.Msal.Utils;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.AppConfig;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.LoggingExtensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.Caching;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Microsoft.Agents.Authentication.Msal
{
    /// <summary>
    /// Authentication class to get access tokens. These tokens in turn are used for signing messages sent 
    /// to Agents, the Azure Bot Service, Teams, and other services. These tokens are also used to validate incoming
    /// data is sent from a trusted source. 
    /// 
    /// This class is used to acquire access tokens using the Microsoft Authentication Library(MSAL).
    /// </summary>
    /// <see href="https://learn.microsoft.com/en-us/entra/identity-platform/msal-overview"/>
    public class MsalAuth : IAccessTokenProvider, IOBOExchange, IMSALProvider, IAgenticTokenProvider
    {
        private readonly static MemoryCache _agenticTokenCache = new MemoryCache(nameof(MsalAuth));

        private readonly MSALHttpClientFactory _msalHttpClient;
        private readonly IServiceProvider _systemServiceProvider;
        private ConcurrentDictionary<Uri, ExecuteAuthenticationResults> _cacheList;
        private readonly ConnectionSettings _connectionSettings;
        private readonly ILogger _logger;
        private readonly ICertificateProvider _certificateProvider;
        private ClientAssertionProviderBase _clientAssertion;

        /// <summary>
        /// Creates a MSAL Authentication Instance. 
        /// </summary>
        /// <param name="systemServiceProvider">Should contain the following objects: a httpClient factory called "MSALClientFactory" and a instance of the MsalAuthConfigurationOptions object</param>
        /// <param name="msalConfigurationSection"></param>
        public MsalAuth(IServiceProvider systemServiceProvider, IConfigurationSection msalConfigurationSection)
            : this(systemServiceProvider, new ConnectionSettings(msalConfigurationSection))
        {
        }

        /// <summary>
        /// Creates a MSAL Authentication Instance. 
        /// </summary>
        /// <param name="systemServiceProvider">Should contain the following objects: a httpClient factory called "MSALClientFactory" and a instance of the MsalAuthConfigurationOptions object</param>
        /// <param name="settings">Settings for this instance.</param>
        public MsalAuth(IServiceProvider systemServiceProvider, ConnectionSettings settings)
        {
            AssertionHelpers.ThrowIfNull(systemServiceProvider, nameof(systemServiceProvider));

            _systemServiceProvider = systemServiceProvider ?? throw new ArgumentNullException(nameof(systemServiceProvider));
            _msalHttpClient = new MSALHttpClientFactory(systemServiceProvider);
            _connectionSettings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = (ILogger)systemServiceProvider.GetService(typeof(ILogger<MsalAuth>));
            _certificateProvider = systemServiceProvider.GetService<ICertificateProvider>() ?? new X509StoreCertificateProvider(_connectionSettings, _logger);
        }

        #region IAccessTokenProvider
        public ImmutableConnectionSettings ConnectionSettings => new(_connectionSettings);

        public async Task<string> GetAccessTokenAsync(string resourceUrl, IList<string> scopes, bool forceRefresh = false)
        {
            var result = await InternalGetAccessTokenAsync(resourceUrl, scopes, forceRefresh).ConfigureAwait(false);
            return result.AccessToken;
        }

        internal async Task<AuthenticationResult> InternalGetAccessTokenAsync(string resourceUrl, IList<string> scopes, bool forceRefresh = false)
        {
            if (!Uri.IsWellFormedUriString(resourceUrl, UriKind.RelativeOrAbsolute))
            {
                throw new ArgumentException("Invalid instance URL");
            }

            Uri instanceUri = new(resourceUrl);
            var localScopes = ResolveScopesList(instanceUri, scopes);

            // Get or create existing token. 
            var cacheEntry = CacheGet(instanceUri, forceRefresh);
            if (cacheEntry != null)
            {
                return cacheEntry.MsalAuthResult;
            }

            object msalAuthClient = InnerCreateClientApplication();

            // setup the result payload. 
            ExecuteAuthenticationResults authResultPayload;
            if (msalAuthClient is IConfidentialClientApplication msalConfidentialClient)
            {
                if (localScopes.Length == 0)
                {
                    throw new ArgumentException("At least one Scope is required for Client Authentication.");
                }

                var authResult = await msalConfidentialClient.AcquireTokenForClient(localScopes).WithForceRefresh(true).ExecuteAsync().ConfigureAwait(false);
                authResultPayload = new ExecuteAuthenticationResults()
                {
                    MsalAuthResult = authResult,
                    TargetServiceUrl = instanceUri,
                    MsalAuthClient = msalAuthClient
                };
            }
            else if (msalAuthClient is IManagedIdentityApplication msalManagedIdentityClient)
            {
                var authResult = await msalManagedIdentityClient.AcquireTokenForManagedIdentity(resourceUrl).WithForceRefresh(true).ExecuteAsync().ConfigureAwait(false);
                authResultPayload = new ExecuteAuthenticationResults()
                {
                    MsalAuthResult = authResult,
                    TargetServiceUrl = instanceUri,
                    MsalAuthClient = msalAuthClient
                };
            }
            else
            {
                throw new System.NotImplementedException();
            }

            CacheSet(instanceUri, authResultPayload);

            return authResultPayload.MsalAuthResult;
        }

        public TokenCredential GetTokenCredential()
        {
            return new MsalTokenCredential(this);
        }
        #endregion

        #region IOBOExchange
        public async Task<TokenResponse> AcquireTokenOnBehalfOf(IEnumerable<string> scopes, string token)
        {
            var msal = InnerCreateClientApplication();
            if (msal is IConfidentialClientApplication confidentialClient)
            {
                var result = await confidentialClient.AcquireTokenOnBehalfOf(scopes, new UserAssertion(token)).ExecuteAsync().ConfigureAwait(false);
                return new TokenResponse() { Token = result.AccessToken, Expiration = result.ExpiresOn.DateTime };
            }

            throw new InvalidOperationException("Only IConfidentialClientApplication is supported for OBO Exchange.");
        }
        #endregion

        #region IMSALProvider
        public IApplicationBase CreateClientApplication()
        {
            return (IApplicationBase)InnerCreateClientApplication();
        }
        #endregion

        #region IAgenticTokenProvider
        public async Task<string> GetAgenticApplicationTokenAsync(string agentAppInstanceId, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(agentAppInstanceId, nameof(agentAppInstanceId));

            if (InnerCreateClientApplication() is IConfidentialClientApplication msalApplication)
            {
                var tokenResult = await msalApplication
                    .AcquireTokenForClient(["api://AzureAdTokenExchange/.default"]).WithFmiPath(agentAppInstanceId)
                    .ExecuteAsync(cancellationToken).ConfigureAwait(false);

                return tokenResult.AccessToken;
            }

            throw new InvalidOperationException("Only IConfidentialClientApplication is supported for Agentic.");
        }

        public async Task<string> GetAgenticInstanceTokenAsync(string agentAppInstanceId, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(agentAppInstanceId, nameof(agentAppInstanceId));

            var agentTokenResult = await GetAgenticApplicationTokenAsync(agentAppInstanceId, cancellationToken).ConfigureAwait(false);

            var instanceApp = ConfidentialClientApplicationBuilder
                .Create(agentAppInstanceId)
                .WithClientAssertion((AssertionRequestOptions options) => Task.FromResult(agentTokenResult))
                .WithAuthority(new Uri(_connectionSettings.Authority ?? $"https://login.microsoftonline.com/{_connectionSettings.TenantId}"))
                .WithLogging(new IdentityLoggerAdapter(_logger), _systemServiceProvider.GetService<IOptions<MsalAuthConfigurationOptions>>().Value.MSALEnabledLogPII)
                .WithLegacyCacheCompatibility(false)
                .WithCacheOptions(new CacheOptions(true))
                .WithHttpClientFactory(_msalHttpClient)
                .Build();

            var agentInstanceToken = await instanceApp
                .AcquireTokenForClient(["api://AzureAdTokenExchange/.default"])
                .ExecuteAsync(cancellationToken).ConfigureAwait(false);

            return agentInstanceToken.AccessToken;
        }

        public async Task<string> GetAgenticUserTokenAsync(string agentAppInstanceId, string agenticUserId, IList<string> scopes, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(agentAppInstanceId, nameof(agentAppInstanceId));
            AssertionHelpers.ThrowIfNullOrWhiteSpace(agenticUserId, nameof(agenticUserId));

            var cacheKey = $"{agentAppInstanceId}/{agenticUserId}/{string.Join(";", scopes)}";
            var value = _agenticTokenCache.Get(cacheKey);
            if (value != null)
            {
                return ((HttpMsalResponse)value).AccessToken;
            }

            var agentToken = await GetAgenticApplicationTokenAsync(agentAppInstanceId, cancellationToken).ConfigureAwait(false);
            var instanceToken = await GetAgenticInstanceTokenAsync(agentAppInstanceId, cancellationToken).ConfigureAwait(false);

            /*
            var instanceApp = ConfidentialClientApplicationBuilder
                .Create(agentAppInstanceId)
                .WithClientAssertion((AssertionRequestOptions options) => Task.FromResult(agentToken))
                .WithAuthority(new Uri(_connectionSettings.Authority ?? $"https://login.microsoftonline.com/{_connectionSettings.TenantId}"))
                .WithExtraQueryParameters(new Dictionary<string,string>()
                    {
                        { "username", upn },
                        { "user_federated_identity_credential", instanceToken },
                        { "grant_type", "user_fic" }
                    })
                .WithLogging(new IdentityLoggerAdapter(_logger), _systemServiceProvider.GetService<IOptions<MsalAuthConfigurationOptions>>().Value.MSALEnabledLogPII)
                .WithLegacyCacheCompatibility(false)
                .WithCacheOptions(new CacheOptions(true))
                .WithHttpClientFactory(_msalHttpClient)
                .Build();

            var aauToken = await instanceApp
                .AcquireTokenForClient(scopes)
                .ExecuteAsync(cancellationToken).ConfigureAwait(false);

            return aauToken.AccessToken;
            */

            var httpClientFactory = _systemServiceProvider.GetService<IHttpClientFactory>();
            using var httpClient = httpClientFactory?.CreateClient(nameof(MsalAuth)) ?? new HttpClient();

            var tokenEndpoint = _connectionSettings.Authority != null 
                ? $"{_connectionSettings.Authority}/oauth2/v2.0/token"
                : $"https://login.microsoftonline.com/{_connectionSettings.TenantId}/oauth2/v2.0/token";

            var parameters = new Dictionary<string, string>
            {
                { "client_id", agentAppInstanceId },
                { "scope", string.Join(" ", scopes) },
                { "client_assertion_type", "urn:ietf:params:oauth:client-assertion-type:jwt-bearer" },
                { "client_assertion", agentToken },
                { "user_id", agenticUserId },
                { "user_federated_identity_credential", instanceToken },
                { "grant_type", "user_fic" }
            };

            var content = new FormUrlEncodedContent(parameters);

            var response = await httpClient.PostAsync(tokenEndpoint, content, cancellationToken).ConfigureAwait(false);

#if !NETSTANDARD
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Failed to acquire Agentic User token: AAI={agentAppInstanceId}, AgenticUserId={agenticUserId}, Scopes={string.Join(";", scopes)}, Response={response.StatusCode}/{responseContent}");
            }

            var acccessTokenResult = System.Text.Json.JsonSerializer.Deserialize<HttpMsalResponse>(responseContent);
            if (string.IsNullOrEmpty(acccessTokenResult.AccessToken))
            {
                throw new InvalidOperationException("Failed to parse access token from response");
            }

            _agenticTokenCache.Add(
                new CacheItem(cacheKey) { Value = acccessTokenResult },
                new CacheItemPolicy()
                {
                    AbsoluteExpiration = DateTime.UtcNow.AddSeconds(acccessTokenResult.ExpiresIn).Subtract(TimeSpan.FromMinutes(5))
                });

            return acccessTokenResult.AccessToken;

            //var tokenResponse = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);
            //if (tokenResponse != null && tokenResponse.TryGetValue("access_token", out var accessToken))
            //{
            //    return accessToken?.ToString() ?? throw new InvalidOperationException("Access token is null");
            //}
            //throw new InvalidOperationException("Failed to parse access token from response");
        }
        #endregion

        private object InnerCreateClientApplication()
        {
            object msalAuthClient = null;

            // check for auth type. 
            if (_connectionSettings.AuthType == AuthTypes.SystemManagedIdentity)
            {
                msalAuthClient = ManagedIdentityApplicationBuilder.Create(ManagedIdentityId.SystemAssigned)
                    .WithLogging(new IdentityLoggerAdapter(_logger), _systemServiceProvider.GetService<IOptions<MsalAuthConfigurationOptions>>().Value.MSALEnabledLogPII)
                    .WithHttpClientFactory(_msalHttpClient)
                    .Build();
            }
            else if (_connectionSettings.AuthType == AuthTypes.UserManagedIdentity)
            {
                msalAuthClient = ManagedIdentityApplicationBuilder.Create(
                        ManagedIdentityId.WithUserAssignedClientId(_connectionSettings.ClientId))
                    .WithLogging(new IdentityLoggerAdapter(_logger), _systemServiceProvider.GetService<IOptions<MsalAuthConfigurationOptions>>().Value.MSALEnabledLogPII)
                    .WithHttpClientFactory(_msalHttpClient)
                    .Build();
            }
            else
            {
                // initialize the MSAL client
                ConfidentialClientApplicationBuilder cAppBuilder = ConfidentialClientApplicationBuilder.CreateWithApplicationOptions(
                new ConfidentialClientApplicationOptions()
                {
                    ClientId = _connectionSettings.ClientId,
                    EnablePiiLogging = _systemServiceProvider.GetService<IOptions<MsalAuthConfigurationOptions>>().Value.MSALEnabledLogPII,
                    LogLevel = Identity.Client.LogLevel.Verbose,
                })
                    .WithLogging(new IdentityLoggerAdapter(_logger), _systemServiceProvider.GetService<IOptions<MsalAuthConfigurationOptions>>().Value.MSALEnabledLogPII)
                    .WithLegacyCacheCompatibility(false)
                    .WithCacheOptions(new CacheOptions(true))
                    .WithHttpClientFactory(_msalHttpClient);


                if (!string.IsNullOrEmpty(_connectionSettings.Authority))
                {
                    cAppBuilder.WithAuthority(_connectionSettings.Authority);
                }
                else
                {
                    cAppBuilder.WithTenantId(_connectionSettings.TenantId);
                }
                // If Client secret was passed in , get the secret and create it that way 
                // if Client CertThumbprint was passed in, get the cert and create it that way.
                // if neither was passed in, throw an exception.
                if (_connectionSettings.AuthType == AuthTypes.Certificate || _connectionSettings.AuthType == AuthTypes.CertificateSubjectName)
                {
                    // Get the certificate from the store
                    cAppBuilder.WithCertificate(_certificateProvider.GetCertificate(), _connectionSettings.SendX5C);
                }
                else if (_connectionSettings.AuthType == AuthTypes.ClientSecret)
                {
                    cAppBuilder.WithClientSecret(_connectionSettings.ClientSecret);
                }
                else if (_connectionSettings.AuthType == AuthTypes.FederatedCredentials)
                {
                    // Reuse this instance so that the assertion is cached and only refreshed once it expires.
                    _clientAssertion = new ManagedIdentityClientAssertion(_connectionSettings.FederatedClientId, null, _logger);

                    cAppBuilder.WithClientAssertion(async (AssertionRequestOptions options) => await _clientAssertion.GetSignedAssertionAsync(_connectionSettings.AssertionRequestOptions));
                }
                else if (_connectionSettings.AuthType == AuthTypes.WorkloadIdentity)
                {
                    // Reuse this instance so that the assertion is cached and only refreshed once it expires.
                    _clientAssertion = new AzureIdentityForKubernetesClientAssertion(_connectionSettings.FederatedTokenFile, _logger);

                    cAppBuilder.WithClientAssertion(async (AssertionRequestOptions options) => await _clientAssertion.GetSignedAssertionAsync(_connectionSettings.AssertionRequestOptions));
                }
                else
                {
                    throw new System.NotImplementedException();
                }

                msalAuthClient = cAppBuilder.Build();
            }

            return msalAuthClient;
        }

        /// <summary>
        /// gets or creates the scope list for the current instance.
        /// </summary>
        /// <param name="instanceUrl"></param>
        /// <param name="scopes">scopes list to create the token for</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        private string[] ResolveScopesList(Uri instanceUrl, IList<string> scopes = null)
        {
            IList<string> _localScopesResolver = new List<string>();

            if (scopes != null && scopes.Count > 0)
            {
                return scopes.ToArray();
            }
            else
            {
                var templist = new List<string>();

                if (_connectionSettings.Scopes != null)
                {
                    foreach (var scope in _connectionSettings.Scopes)
                    {
                        var scopePlaceholder = scope;
#if !NETSTANDARD
                        if (scopePlaceholder.Contains("{instance}", StringComparison.CurrentCultureIgnoreCase))
#else
                        if (scopePlaceholder.ToString().Contains("{instance}"))
#endif
                        {
                            scopePlaceholder = scopePlaceholder.Replace("{instance}", $"{instanceUrl.Scheme}://{instanceUrl.Host}");
                        }
                        templist.Add(scopePlaceholder);
                    }
                }
                return templist.ToArray();
            }
        }

        private ExecuteAuthenticationResults CacheGet(Uri instanceUri, bool forceRefresh = false)
        {
            _cacheList ??= new ConcurrentDictionary<Uri, ExecuteAuthenticationResults>();
            if (_cacheList.TryGetValue(instanceUri, out ExecuteAuthenticationResults authResultFromCache))
            {
                if (!forceRefresh)
                {
                    var accessToken = authResultFromCache.MsalAuthResult.AccessToken;
                    var tokenExpiresOn = authResultFromCache.MsalAuthResult.ExpiresOn;
                    if (tokenExpiresOn != null && tokenExpiresOn < DateTimeOffset.UtcNow.Subtract(TimeSpan.FromSeconds(30)))
                    {
                        // flush the access token if it is about to expire.
#if !NETSTANDARD
                        _cacheList.Remove(instanceUri, out ExecuteAuthenticationResults _);
#else
                        _cacheList.TryRemove(instanceUri, out ExecuteAuthenticationResults _);
#endif
                        return null;
                    }

                    return authResultFromCache;
                }
                else
                {
#if !NETSTANDARD
                    _cacheList.Remove(instanceUri, out ExecuteAuthenticationResults _);
#else
                    _cacheList.TryRemove(instanceUri, out ExecuteAuthenticationResults _);
#endif
                }
            }

            return null;
        }

        private void CacheSet(Uri instanceUri, ExecuteAuthenticationResults authResultPayload)
        {
            if (_cacheList.ContainsKey(instanceUri))
            {
                _cacheList[instanceUri] = authResultPayload;
            }
            else
            {
                _cacheList.TryAdd(instanceUri, authResultPayload);
            }
        }
    }

    class HttpMsalResponse
    {
        [JsonPropertyName("token_type")]
        public string TokeType { get; set; }

        [JsonPropertyName("scope")]
        public string Scope { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    
        [JsonPropertyName("ext_expires_in")]
        public int ExtExpiresIn { get; set; }

        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }
    }
}
