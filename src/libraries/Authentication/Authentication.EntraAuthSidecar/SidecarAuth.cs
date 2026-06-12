// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Core;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Authentication.EntraAuthSidecar.Model;
using Microsoft.Agents.Authentication.EntraAuthSidecar.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Authentication.EntraAuthSidecar
{
    /// <summary>
    /// Connection-level token provider that delegates token acquisition to the Microsoft Entra SDK
    /// for Agent ID sidecar. This replaces MSAL at the connection layer, using the sidecar's
    /// <c>/AuthorizationHeaderUnauthenticated/{serviceName}</c> endpoint for app-only and
    /// autonomous/delegated agent identity flows.
    /// </summary>
    public class SidecarAuth : IAccessTokenProvider, IAgenticTokenProvider
    {
        private readonly SidecarHttpClient _sidecarClient;
        private readonly SidecarConnectionSettings _settings;

        // Lightweight in-memory token cache, keyed by the agent identity (client id) plus the
        // remaining request parameters that affect the issued token. Mirrors the MSAL provider's
        // SDK-side cache. Eviction is lazy on read (CacheGet drops an entry once it is at/near expiry),
        // and a hard upper bound is enforced on write (CacheSet): expired entries are reclaimed first,
        // then the entries nearest to expiry are evicted so the cache never exceeds MaxCacheEntries.
        private readonly ConcurrentDictionary<string, CachedToken> _tokenCache = new();

        // Refresh slightly ahead of the real expiry so callers never receive a token that expires mid-flight.
        private static readonly TimeSpan ExpiryBuffer = TimeSpan.FromSeconds(30);

        // Hard upper bound on cached entries; protects memory when many distinct identities are served.
        private const int MaxCacheEntries = 500;

        /// <summary>
        /// Creates a new <see cref="SidecarAuth"/> using DI service provider.
        /// This constructor matches the <c>(IServiceProvider, IConfigurationSection)</c> signature
        /// required by the <see cref="Authentication.ConfigurationConnections"/> module loader,
        /// allowing this provider to be used as a connection-level token provider in config.
        /// </summary>
        /// <param name="serviceProvider">The DI service provider.</param>
        /// <param name="configurationSection">Configuration section with sidecar connection settings.</param>
        public SidecarAuth(IServiceProvider serviceProvider, IConfigurationSection configurationSection)
            : this(
                  CreateSidecarHttpClient(serviceProvider, configurationSection),
                  configurationSection?.Get<SidecarConnectionSettings>() ?? new SidecarConnectionSettings())
        {
        }

        /// <summary>
        /// Creates a new <see cref="SidecarAuth"/> from configuration.
        /// </summary>
        /// <param name="sidecarClient">The shared sidecar HTTP client.</param>
        /// <param name="configurationSection">Configuration section with sidecar connection settings.</param>
        internal SidecarAuth(SidecarHttpClient sidecarClient, IConfigurationSection configurationSection)
            : this(sidecarClient, configurationSection?.Get<SidecarConnectionSettings>() ?? new SidecarConnectionSettings())
        {
        }

        /// <summary>
        /// Creates a new <see cref="SidecarAuth"/> with explicit settings.
        /// </summary>
        /// <param name="sidecarClient">The shared sidecar HTTP client.</param>
        /// <param name="settings">The sidecar connection settings.</param>
        internal SidecarAuth(SidecarHttpClient sidecarClient, SidecarConnectionSettings settings)
        {
            _sidecarClient = sidecarClient ?? throw new ArgumentNullException(nameof(sidecarClient));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <inheritdoc/>
        public ImmutableConnectionSettings ConnectionSettings => new(_settings);

        /// <summary>
        /// Creates a <see cref="SidecarHttpClient"/> from the DI service provider and configuration section.
        /// Uses IHttpClientFactory if registered, otherwise falls back to a new HttpClient instance.
        /// </summary>
        private static SidecarHttpClient CreateSidecarHttpClient(IServiceProvider serviceProvider, IConfigurationSection configurationSection)
        {
            // Prefer an already-registered (and already-validated) client from DI. When a safe,
            // preconfigured SidecarHttpClient exists we reuse it and skip resolving/validating the
            // config or SIDECAR_URL value, which may be absent or intentionally point elsewhere.
            var existingClient = serviceProvider?.GetService<SidecarHttpClient>();
            if (existingClient != null)
            {
                return existingClient;
            }

            var configuredBaseUrl = configurationSection?.GetValue<string>("SidecarBaseUrl");
            var bypassLocalNetworkRestriction = configurationSection?.GetValue("BypassLocalNetworkRestriction", false) ?? false;
            var resolvedUrl = SidecarHttpClient.ResolveBaseUrl(configuredBaseUrl);

            // SSRF safety: the resolved URL (config or SIDECAR_URL env) must point to a loopback/private
            // address before we send the agent's credentials to it, unless the operator has explicitly
            // opted out via BypassLocalNetworkRestriction.
            SidecarHttpClient.ValidateBaseUrl(resolvedUrl, bypassLocalNetworkRestriction);

            var requestTimeout = configurationSection?.GetValue<TimeSpan?>("RequestTimeout") ?? SidecarHttpClient.DefaultTimeout;
            var retryCount = configurationSection?.GetValue<int?>("RetryCount") ?? SidecarHttpClient.DefaultRetryCount;

            var logger = serviceProvider?.GetService<ILoggerFactory>()?.CreateLogger<SidecarHttpClient>()
                ?? (ILogger)NullLogger<SidecarHttpClient>.Instance;

            // Fall back to creating one using IHttpClientFactory or a plain HttpClient
            var httpClientFactory = serviceProvider?.GetService<IHttpClientFactory>();
            HttpClient httpClient = httpClientFactory != null
                ? httpClientFactory.CreateClient(SidecarHttpClient.HttpClientName)
                : new HttpClient { Timeout = requestTimeout };

            return new SidecarHttpClient(httpClient, resolvedUrl, logger, requestTimeout, retryCount);
        }

        /// <inheritdoc/>
        public async Task<string> GetAccessTokenAsync(string resourceUrl, IList<string> scopes, bool forceRefresh = false)
        {
            // Connection-level app-only token. The sidecar owns the agent credential and returns an
            // app token for the configured downstream API / overridden scopes.
            var options = new SidecarRequestOptions
            {
                Scopes = scopes,
                RequestAppToken = true,
                ForceRefresh = forceRefresh ? true : null
            };

            return await GetCachedTokenAsync(
                _settings.ServiceName ?? "default",
                options).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public TokenCredential GetTokenCredential()
        {
            return new SidecarTokenCredential(this);
        }

        /// <inheritdoc/>
        public async Task<string> GetAgenticApplicationTokenAsync(string tenantId, string agentAppInstanceId, CancellationToken cancellationToken = default)
        {
            // Blueprint (agent application) token. The sidecar owns the agent credential and returns a
            // token-exchange-scoped token bound to the agent instance (the federated managed identity
            // path), via a dedicated downstream API configured app-only with the
            // api://AzureAdTokenExchange/.default scope. This is the credential-free replacement for
            // MSAL's AcquireTokenForClient(["api://AzureAdTokenExchange/.default"]).WithFmiPath(agentAppInstanceId).
            // Scope / RequestAppToken are sidecar-side configuration and are not overridden here.
            var options = new SidecarRequestOptions
            {
                AgentIdentity = agentAppInstanceId,
                Tenant = tenantId
            };

            return await GetCachedTokenAsync(
                _settings.BlueprintServiceName ?? "agenticblueprint",
                options,
                cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<string> GetAgenticInstanceTokenAsync(string tenantId, string agentAppInstanceId, CancellationToken cancellationToken = default)
        {
            // Autonomous agent (instance) token for the configured resource. The sidecar performs the
            // full Blueprint -> Instance chain internally and returns an app-only resource token.
            // Requires the downstream API to be configured app-only (RequestAppToken) on the sidecar.
            var options = new SidecarRequestOptions
            {
                AgentIdentity = agentAppInstanceId,
                RequestAppToken = true,
                Tenant = tenantId,
                Scopes = _settings.Scopes
            };

            return await GetCachedTokenAsync(
                _settings.ServiceName ?? "default",
                options,
                cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<string> GetAgenticUserTokenAsync(string tenantId, string agentAppInstanceId, string upn, IList<string> scopes, CancellationToken cancellationToken = default)
        {
            // Agentic user token for the configured resource. The sidecar performs the full agentic
            // identity chain internally (Blueprint -> Instance -> agentic User via federated identity)
            // and returns the resource token for the agentic user. The agentic user is identified by
            // object id (AgentUserId) when "upn" is a GUID, otherwise by UPN (AgentUsername).
            var isObjectId = Guid.TryParse(upn, out _);

            var options = new SidecarRequestOptions
            {
                AgentIdentity = agentAppInstanceId,
                AgentUsername = isObjectId ? null : upn,
                AgentUserId = isObjectId ? upn : null,
                Tenant = tenantId,
                Scopes = scopes ?? _settings.Scopes
            };

            return await GetCachedTokenAsync(
                _settings.ServiceName ?? "default",
                options,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Acquires a token from the sidecar, serving it from the in-memory cache when a valid
        /// (non-expired) entry exists for the same request parameters. When the request opts into
        /// <see cref="SidecarRequestOptions.ForceRefresh"/>, any cached entry is evicted and a fresh
        /// token is acquired.
        /// </summary>
        private async Task<string> GetCachedTokenAsync(string serviceName, SidecarRequestOptions options, CancellationToken cancellationToken = default)
        {
            var forceRefresh = options.ForceRefresh == true;
            var cacheKey = BuildCacheKey(serviceName, options);

            var cached = CacheGet(cacheKey, forceRefresh);
            if (cached != null)
            {
                return cached.Token;
            }

            var result = await _sidecarClient.GetAuthorizationHeaderUnauthenticatedAsync(
                serviceName,
                options,
                cancellationToken).ConfigureAwait(false);

            CacheSet(cacheKey, new CachedToken(result.Token, SidecarTokenExpiry.Resolve(result.Token)));
            return result.Token;
        }

        private CachedToken CacheGet(string cacheKey, bool forceRefresh)
        {
            if (_tokenCache.TryGetValue(cacheKey, out var cached))
            {
                if (!forceRefresh && cached.ExpiresOn >= DateTimeOffset.UtcNow.Add(ExpiryBuffer))
                {
                    return cached;
                }

                // Token is being force-refreshed or is at/near expiry; flush it.
                _tokenCache.TryRemove(cacheKey, out _);
            }

            return null;
        }

        private void CacheSet(string cacheKey, CachedToken token)
        {
            _tokenCache[cacheKey] = token;

            // Enforce a hard upper bound on the cache. First reclaim expired entries (cheap, the common
            // case). If the cache is still over the cap because many cached tokens are still valid, evict
            // the entries nearest to expiry so memory stays bounded even when an agent serves a high
            // cardinality of distinct users/tenants/scopes over its lifetime.
            if (_tokenCache.Count > MaxCacheEntries)
            {
                PruneExpiredEntries();

                if (_tokenCache.Count > MaxCacheEntries)
                {
                    EvictNearestExpiry();
                }
            }
        }

        private void PruneExpiredEntries()
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var entry in _tokenCache)
            {
                if (entry.Value.ExpiresOn <= now)
                {
                    _tokenCache.TryRemove(entry.Key, out _);
                }
            }
        }

        private void EvictNearestExpiry()
        {
            var overflow = _tokenCache.Count - MaxCacheEntries;
            if (overflow <= 0)
            {
                return;
            }

            foreach (var entry in _tokenCache.OrderBy(e => e.Value.ExpiresOn).Take(overflow))
            {
                _tokenCache.TryRemove(entry.Key, out _);
            }
        }

        /// <summary>
        /// Builds the cache key. Keyed primarily by the agent identity (client id), plus the remaining
        /// request parameters that change the issued token, so distinct flows, users, tenants, and
        /// scopes never collide. <see cref="SidecarRequestOptions.ForceRefresh"/> is intentionally
        /// excluded so a forced refresh updates the same entry.
        /// </summary>
        private static string BuildCacheKey(string serviceName, SidecarRequestOptions options)
        {
            var scopes = options.Scopes != null ? string.Join(" ", options.Scopes) : string.Empty;
            return string.Join("|",
                serviceName,
                options.AgentIdentity,
                options.AgentUsername,
                options.AgentUserId,
                options.Tenant,
                options.RequestAppToken == true ? "app" : "user",
                scopes);
        }

        private sealed class CachedToken(string token, DateTimeOffset expiresOn)
        {
            public string Token { get; } = token;

            public DateTimeOffset ExpiresOn { get; } = expiresOn;
        }
    }
}
