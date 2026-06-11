// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Core;
using Microsoft.Agents.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.UserAuth.EntraSidecar
{
    /// <summary>
    /// Connection-level token provider that delegates token acquisition to the Microsoft Entra SDK
    /// for Agent ID sidecar. This replaces MSAL at the connection layer, using the sidecar's
    /// <c>/AuthorizationHeaderUnauthenticated/{serviceName}</c> endpoint for app-only and
    /// autonomous/delegated agent identity flows.
    /// </summary>
    public class SidecarAccessTokenProvider : IAccessTokenProvider, IAgenticTokenProvider
    {
        private readonly SidecarHttpClient _sidecarClient;
        private readonly SidecarConnectionSettings _settings;

        /// <summary>
        /// Creates a new <see cref="SidecarAccessTokenProvider"/> using DI service provider.
        /// This constructor matches the <c>(IServiceProvider, IConfigurationSection)</c> signature
        /// required by the <see cref="Authentication.ConfigurationConnections"/> module loader,
        /// allowing this provider to be used as a connection-level token provider in config.
        /// </summary>
        /// <param name="serviceProvider">The DI service provider.</param>
        /// <param name="configurationSection">Configuration section with sidecar connection settings.</param>
        public SidecarAccessTokenProvider(IServiceProvider serviceProvider, IConfigurationSection configurationSection)
            : this(
                  CreateSidecarHttpClient(serviceProvider, configurationSection),
                  configurationSection?.Get<SidecarConnectionSettings>() ?? new SidecarConnectionSettings())
        {
        }

        /// <summary>
        /// Creates a new <see cref="SidecarAccessTokenProvider"/> from configuration.
        /// </summary>
        /// <param name="sidecarClient">The shared sidecar HTTP client.</param>
        /// <param name="configurationSection">Configuration section with sidecar connection settings.</param>
        internal SidecarAccessTokenProvider(SidecarHttpClient sidecarClient, IConfigurationSection configurationSection)
            : this(sidecarClient, configurationSection?.Get<SidecarConnectionSettings>() ?? new SidecarConnectionSettings())
        {
        }

        /// <summary>
        /// Creates a new <see cref="SidecarAccessTokenProvider"/> with explicit settings.
        /// </summary>
        /// <param name="sidecarClient">The shared sidecar HTTP client.</param>
        /// <param name="settings">The sidecar connection settings.</param>
        internal SidecarAccessTokenProvider(SidecarHttpClient sidecarClient, SidecarConnectionSettings settings)
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
            var baseUrl = configurationSection?.GetValue<string>("SidecarBaseUrl");
            var resolvedUrl = SidecarHttpClient.ResolveBaseUrl(baseUrl);

            var logger = serviceProvider?.GetService<ILoggerFactory>()?.CreateLogger<SidecarHttpClient>()
                ?? (ILogger)NullLogger<SidecarHttpClient>.Instance;

            // Try to get from DI first (registered via AddSidecarConnections)
            var existingClient = serviceProvider?.GetService<SidecarHttpClient>();
            if (existingClient != null)
            {
                return existingClient;
            }

            // Fall back to creating one using IHttpClientFactory or a plain HttpClient
            var httpClientFactory = serviceProvider?.GetService<IHttpClientFactory>();
            HttpClient httpClient = httpClientFactory != null
                ? httpClientFactory.CreateClient(SidecarHttpClient.HttpClientName)
                : new HttpClient { Timeout = SidecarHttpClient.DefaultTimeout };

            return new SidecarHttpClient(httpClient, resolvedUrl, logger);
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

            var result = await _sidecarClient.GetAuthorizationHeaderUnauthenticatedAsync(
                _settings.ServiceName ?? "default",
                options).ConfigureAwait(false);

            return result.Token;
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

            var result = await _sidecarClient.GetAuthorizationHeaderUnauthenticatedAsync(
                _settings.BlueprintServiceName ?? "agenticblueprint",
                options,
                cancellationToken).ConfigureAwait(false);

            return result.Token;
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

            var result = await _sidecarClient.GetAuthorizationHeaderUnauthenticatedAsync(
                _settings.ServiceName ?? "default",
                options,
                cancellationToken).ConfigureAwait(false);

            return result.Token;
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

            var result = await _sidecarClient.GetAuthorizationHeaderUnauthenticatedAsync(
                _settings.ServiceName ?? "default",
                options,
                cancellationToken).ConfigureAwait(false);

            return result.Token;
        }
    }
}
