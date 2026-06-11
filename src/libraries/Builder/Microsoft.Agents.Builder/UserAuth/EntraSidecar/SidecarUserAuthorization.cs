// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Builder.Telemetry.Authorization.Scopes;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
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
    /// Handles user OAuth token acquisition via the Microsoft Entra SDK for Agent ID sidecar.
    /// In Phase 1 this calls the sidecar's <c>/AuthorizationHeaderUnauthenticated/{serviceName}</c> endpoint
    /// with agent identity parameters derived from <c>Activity.Recipient</c>; the sidecar resolves the full
    /// agentic token chain (Blueprint → Instance → User) internally.
    /// </summary>
    /// <remarks>
    /// This is for agentic requests only, where the agent identity information is available
    /// in Activity.Recipient (AgenticAppId, AgenticUserId, Id).
    /// </remarks>
    public class SidecarUserAuthorization : IUserAuthorization
    {
        private readonly IConnections _connections;
        private readonly SidecarSettings _settings;
        private readonly ILogger _logger;
        private readonly SidecarHttpClient _sidecarClient;

        /// <summary>
        /// Required constructor for type loader construction (when using IConfiguration).
        /// </summary>
        /// <param name="name">The authentication handler name.</param>
        /// <param name="storage">The storage provider (unused but required by the loader interface).</param>
        /// <param name="connections">The connections provider.</param>
        /// <param name="configurationSection">Configuration section containing <see cref="SidecarSettings"/>.</param>
        /// <param name="logger">Optional logger.</param>
        /// <param name="serviceProvider">The service provider for resolving additional services.</param>
#pragma warning disable IDE0060 // Remove unused parameter
        public SidecarUserAuthorization(string name, IStorage storage, IConnections connections, IConfigurationSection configurationSection, ILogger logger = null, IServiceProvider serviceProvider = null)
#pragma warning restore IDE0060 // Remove unused parameter
            : this(name, connections, configurationSection.Get<SidecarSettings>(), serviceProvider?.GetService<IHttpClientFactory>(), logger)
        {
        }

        /// <summary>
        /// Code-first constructor.
        /// </summary>
        /// <param name="name">The authentication handler name.</param>
        /// <param name="connections">The connections provider.</param>
        /// <param name="settings">The Entra Sidecar settings.</param>
        /// <param name="httpClientFactory">Optional HTTP client factory. If null, a default HttpClient is created.</param>
        /// <param name="logger">Optional logger.</param>
        public SidecarUserAuthorization(string name, IConnections connections, SidecarSettings settings, IHttpClientFactory httpClientFactory = null, ILogger logger = null)
        {
            AssertionHelpers.ThrowIfNull(connections, nameof(connections));

            _connections = connections;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? NullLogger<ILogger>.Instance;

            if (string.IsNullOrEmpty(_settings.ServiceName))
            {
                throw new ArgumentException("ServiceName is required.", nameof(settings));
            }

            var httpClient = httpClientFactory?.CreateClient(SidecarHttpClient.HttpClientName)
                ?? new HttpClient { Timeout = SidecarHttpClient.DefaultTimeout };
            _sidecarClient = new SidecarHttpClient(httpClient, _settings.ResolvedSidecarBaseUrl);
        }

        /// <summary>
        /// Constructor that accepts a SidecarHttpClient directly for testability.
        /// </summary>
        internal SidecarUserAuthorization(string name, IConnections connections, SidecarSettings settings, SidecarHttpClient sidecarClient, ILogger logger = null)
        {
            AssertionHelpers.ThrowIfNull(connections, nameof(connections));

            _connections = connections;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? NullLogger<ILogger>.Instance;
            _sidecarClient = sidecarClient ?? throw new ArgumentNullException(nameof(sidecarClient));
        }

        /// <inheritdoc/>
        public string Name { get; private set; }

        /// <inheritdoc/>
        public Task<TokenResponse> SignInUserAsync(ITurnContext context, bool forceSignIn = false, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            return GetRefreshedUserTokenAsync(context, exchangeConnection, exchangeScopes, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<TokenResponse> GetRefreshedUserTokenAsync(ITurnContext turnContext, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            if (!turnContext.Activity.IsAgenticRequest())
            {
                throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.NotAnAgenticRequest, null, "GetEntraSidecarUserToken");
            }

            using var telemetryScope = new ScopeAgenticToken(Name, exchangeConnection, exchangeScopes);

            // Build request options from the activity's agent identity context.
            // The sidecar handles the full agentic token chain (Blueprint → Instance → User)
            // internally using the AgentIdentity/AgentUserId query params.
            var options = BuildRequestOptions(turnContext.Activity, exchangeScopes);

            try
            {
                // The sidecar resolves the agentic chain (Blueprint → Instance → User) from the
                // agent identity parameters supplied on the request.
                var result = await _sidecarClient.GetAuthorizationHeaderUnauthenticatedAsync(
                    _settings.ServiceName,
                    options,
                    cancellationToken).ConfigureAwait(false);

                return new TokenResponse(token: result.Token);
            }
            catch (SidecarRequestException ex)
            {
                _logger.LogError("Entra Sidecar returned {StatusCode} for service '{ServiceName}': {Error}",
                    ex.StatusCode, _settings.ServiceName, ex.RawContent);
                throw ExceptionHelper.GenerateException<InvalidOperationException>(
                    ErrorHelper.EntraSidecarTokenAcquisitionFailed, null,
                    _settings.ServiceName, ex.StatusCode.ToString(), ex.Message);
            }
        }

        /// <inheritdoc/>
        public Task ResetStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            // Intentional no-op in Phase 1: the sidecar owns the token cache and exposes no per-user
            // reset endpoint, and this handler holds no SDK-side token state to clear. This is the
            // integration point for a future sidecar reset/revoke operation.
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task SignOutUserAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            // Intentional no-op in Phase 1: the sidecar owns the token cache and exposes no revoke
            // endpoint, and this handler holds no SDK-side token state to clear. This is the integration
            // point for a future sidecar revoke operation.
            return Task.CompletedTask;
        }

        private SidecarRequestOptions BuildRequestOptions(IActivity activity, IList<string> exchangeScopes)
        {
            var options = new SidecarRequestOptions
            {
                ForceRefresh = _settings.ForceRefresh ? true : null,
                RequestAppToken = _settings.RequestAppToken ? true : null,
                // AgentIdentity from Recipient.AgenticAppId
                AgentIdentity = activity.Recipient?.AgenticAppId
            };

            // For AgenticUser role, add either AgentUserId or AgentUsername
            if (string.Equals(activity.Recipient?.Role, RoleTypes.AgenticUser, StringComparison.OrdinalIgnoreCase))
            {
                var agentUserId = activity.Recipient?.AgenticUserId;
                if (!string.IsNullOrEmpty(agentUserId))
                {
                    options.AgentUserId = agentUserId;
                }
                else
                {
                    options.AgentUsername = activity.Recipient?.Id;
                }
            }

            // Scope overrides
            options.Scopes = exchangeScopes ?? _settings.Scopes;

            // Tenant override
            if (!string.IsNullOrEmpty(_settings.Tenant))
            {
                options.Tenant = _settings.Tenant;
            }

            return options;
        }
    }
}
