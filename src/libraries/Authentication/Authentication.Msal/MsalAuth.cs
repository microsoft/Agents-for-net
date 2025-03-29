// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication.Msal.Model;
using Microsoft.Agents.Authentication.Msal.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.AppConfig;
using Microsoft.IdentityModel.LoggingExtensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
    public class MsalAuth : IAccessTokenProvider, IOBOExchange, IMSALProvider
    {
        private readonly MSALHttpClientFactory _msalHttpClient;
        private readonly IServiceProvider _systemServiceProvider;
        private ConcurrentDictionary<Uri, ExecuteAuthenticationResults> _cacheList;
        private readonly ConnectionSettings _connectionSettings;
        private readonly ILogger _logger;
        private readonly ICertificateProvider _certificateProvider;

        /// <summary>
        /// Creates a MSAL Authentication Instance. 
        /// </summary>
        /// <param name="msalConfigurationSection"></param>
        /// <param name="systemServiceProvider">Should contain the following objects: a httpClient factory called "MSALClientFactory" and a instance of the MsalAuthConfigurationOptions object</param>
        public MsalAuth(IServiceProvider systemServiceProvider, IConfigurationSection msalConfigurationSection)
        {
            ArgumentNullException.ThrowIfNull(systemServiceProvider, nameof(systemServiceProvider));
            ArgumentNullException.ThrowIfNull(msalConfigurationSection, nameof(msalConfigurationSection));

            _systemServiceProvider = systemServiceProvider;
            _msalHttpClient = new MSALHttpClientFactory(systemServiceProvider);
            _connectionSettings = new ConnectionSettings(msalConfigurationSection);
            _logger = (ILogger)systemServiceProvider.GetService(typeof(ILogger<MsalAuth>));
            _certificateProvider = systemServiceProvider.GetService<ICertificateProvider>() ?? new X509StoreCertificateProvider(_connectionSettings, _logger);
        }

        public async Task<string> GetAccessTokenAsync(string resourceUrl, IList<string> scopes, bool forceRefresh = false)
        {
            if (!Uri.IsWellFormedUriString(resourceUrl, UriKind.RelativeOrAbsolute))
            {
                throw new ArgumentException("Invalid instance URL");
            }

            Uri instanceUri = new(resourceUrl);
            var localScopes = ResolveScopesList(instanceUri, scopes);

            // Get or create existing token. 
            _cacheList ??= new ConcurrentDictionary<Uri, ExecuteAuthenticationResults>();
            if (_cacheList.ContainsKey(instanceUri))
            {
                if (!forceRefresh)
                {
                    var accessToken = _cacheList[instanceUri].MsalAuthResult.AccessToken;
                    var tokenExpiresOn = _cacheList[instanceUri].MsalAuthResult.ExpiresOn;
                    if (tokenExpiresOn != null && tokenExpiresOn < DateTimeOffset.UtcNow.Subtract(TimeSpan.FromSeconds(30)))
                    {
                        accessToken = string.Empty; // flush the access token if it is about to expire.
                        _cacheList.Remove(instanceUri, out ExecuteAuthenticationResults _);
                    }

                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        return accessToken;
                    }
                }
                else
                {
                    _cacheList.Remove(instanceUri, out ExecuteAuthenticationResults _);
                }
            }

            object msalAuthClient = InnerCreateClientApplication();

            // setup the result payload. 
            ExecuteAuthenticationResults authResultPayload = null; 
            if (msalAuthClient is IConfidentialClientApplication msalConfidentialClient)
            {
                if (localScopes.Length == 0) {
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

            if (_cacheList.ContainsKey(instanceUri))
            {
                _cacheList[instanceUri] = authResultPayload;
            }
            else
            {
                _cacheList.TryAdd(instanceUri, authResultPayload);
            }

            return authResultPayload.MsalAuthResult.AccessToken;
        }


        public async Task<string> AcquireTokenOnBehalfOf(IEnumerable<string> scopes, string token)
        {
            var msal = InnerCreateClientApplication();
            if (msal is IConfidentialClientApplication confidentialClient)
            {
                var result = await confidentialClient.AcquireTokenOnBehalfOf(scopes, new UserAssertion(token)).ExecuteAsync().ConfigureAwait(false);
                return result.AccessToken;
            }

            throw new InvalidOperationException("Only IConfidentialClientApplication is supported for OBO Exchange.");
        }

        public IApplicationBase CreateClientApplication()
        {
            return (IApplicationBase)InnerCreateClientApplication();
        }

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
        /// <exception cref="ArgumentNullException"></exception>
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
                        if (scopePlaceholder.ToLower().Contains("{instance}"))
                        {
                            scopePlaceholder = scopePlaceholder.Replace("{instance}", $"{instanceUrl.Scheme}://{instanceUrl.Host}");
                        }
                        templist.Add(scopePlaceholder);
                    }
                }
                return templist.ToArray();
            }
        }
    }
}
