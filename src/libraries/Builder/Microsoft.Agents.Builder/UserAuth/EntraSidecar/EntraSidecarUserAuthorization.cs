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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.UserAuth.EntraSidecar
{
    /// <summary>
    /// Handles user OAuth token acquisition via the Microsoft Entra SDK for Agent ID sidecar.
    /// This calls the sidecar's /AuthorizationHeader/{serviceName} endpoint with agent identity
    /// parameters derived from Activity.Recipient.
    /// </summary>
    /// <remarks>
    /// This is for agentic requests only, where the agent identity information is available
    /// in Activity.Recipient (AgenticAppId, AgenticUserId, Id).
    /// </remarks>
    public class EntraSidecarUserAuthorization : IUserAuthorization
    {
        private readonly IConnections _connections;
        private readonly EntraSidecarSettings _settings;
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Required constructor for type loader construction (when using IConfiguration).
        /// </summary>
        /// <param name="name">The authentication handler name.</param>
        /// <param name="storage">The storage provider (unused but required by the loader interface).</param>
        /// <param name="connections">The connections provider for retrieving the inbound user token.</param>
        /// <param name="configurationSection">Configuration section containing <see cref="EntraSidecarSettings"/>.</param>
        /// <param name="logger">Optional logger.</param>
        /// <param name="serviceProvider">The service provider for resolving additional services.</param>
        public EntraSidecarUserAuthorization(string name, IStorage storage, IConnections connections, IConfigurationSection configurationSection, ILogger logger = null, IServiceProvider serviceProvider = null)
            : this(name, connections, configurationSection.Get<EntraSidecarSettings>(), serviceProvider?.GetService<IHttpClientFactory>(), logger)
        {
        }

        /// <summary>
        /// Code-first constructor.
        /// </summary>
        /// <param name="name">The authentication handler name.</param>
        /// <param name="connections">The connections provider for retrieving the inbound user token.</param>
        /// <param name="settings">The Entra Sidecar settings.</param>
        /// <param name="httpClientFactory">Optional HTTP client factory. If null, a default HttpClient is created.</param>
        /// <param name="logger">Optional logger.</param>
        public EntraSidecarUserAuthorization(string name, IConnections connections, EntraSidecarSettings settings, IHttpClientFactory httpClientFactory = null, ILogger logger = null)
        {
            AssertionHelpers.ThrowIfNull(connections, nameof(connections));

            _connections = connections;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? NullLogger<ILogger>.Instance;

            if (string.IsNullOrEmpty(_settings.SidecarBaseUrl))
            {
                throw new ArgumentException("SidecarBaseUrl is required.", nameof(settings));
            }

            if (string.IsNullOrEmpty(_settings.ServiceName))
            {
                throw new ArgumentException("ServiceName is required.", nameof(settings));
            }

            _httpClient = httpClientFactory?.CreateClient(nameof(EntraSidecarUserAuthorization)) ?? new HttpClient();
        }

        /// <summary>
        /// Constructor that accepts an HttpClient directly for testability.
        /// </summary>
        internal EntraSidecarUserAuthorization(string name, IConnections connections, EntraSidecarSettings settings, HttpClient httpClient, ILogger logger = null)
        {
            AssertionHelpers.ThrowIfNull(connections, nameof(connections));

            _connections = connections;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? NullLogger<ILogger>.Instance;
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
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

            // Get the inbound user token via the connection infrastructure
            string inboundToken = await GetInboundTokenAsync(turnContext, cancellationToken).ConfigureAwait(false);

            // Build the sidecar request URL with agent identity query parameters
            string requestUrl = BuildSidecarRequestUrl(turnContext.Activity, exchangeScopes);

            // Call the sidecar
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            if (!string.IsNullOrEmpty(inboundToken))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", inboundToken);
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
#if NETSTANDARD
                var errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#else
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#endif
                _logger.LogError("Entra Sidecar returned {StatusCode} for service '{ServiceName}': {Error}", (int)response.StatusCode, _settings.ServiceName, errorContent);
                throw ExceptionHelper.GenerateException<InvalidOperationException>(
                    ErrorHelper.EntraSidecarTokenAcquisitionFailed, null, _settings.ServiceName, ((int)response.StatusCode).ToString(), errorContent);
            }

            // Parse the response: { "authorizationHeader": "Bearer eyJ..." }
#if NETSTANDARD
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#else
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#endif
            string token = ExtractTokenFromResponse(responseContent);

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

        private async Task<string> GetInboundTokenAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            IAccessTokenProvider connection;
            if (!string.IsNullOrEmpty(_settings.AlternateBlueprintConnectionName))
            {
                connection = _connections.GetConnection(_settings.AlternateBlueprintConnectionName);
            }
            else
            {
                connection = _connections.GetTokenProvider(turnContext.Identity, turnContext.Activity);
            }

            if (connection is not IAgenticTokenProvider agenticTokenProvider)
            {
                throw ExceptionHelper.GenerateException<InvalidOperationException>(
                    ErrorHelper.AgenticTokenProviderNotFound, null, $"{turnContext.Identity.GetIncomingAudience()}:{turnContext.Activity.ServiceUrl}");
            }

            // Get the inbound token based on the role
            if (string.Equals(turnContext.Activity?.Recipient?.Role, RoleTypes.AgenticIdentity, StringComparison.OrdinalIgnoreCase))
            {
                return await agenticTokenProvider.GetAgenticInstanceTokenAsync(
                    turnContext.Activity.GetAgenticTenantId(),
                    turnContext.Activity.GetAgenticInstanceId(),
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                return await agenticTokenProvider.GetAgenticUserTokenAsync(
                    turnContext.Activity.GetAgenticTenantId(),
                    turnContext.Activity.GetAgenticInstanceId(),
                    turnContext.Activity.GetAgenticUser(),
                    null,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        private string BuildSidecarRequestUrl(IActivity activity, IList<string> exchangeScopes)
        {
            var baseUrl = _settings.SidecarBaseUrl.TrimEnd('/');
            var url = $"{baseUrl}/AuthorizationHeader/{Uri.EscapeDataString(_settings.ServiceName)}";

            var queryParams = new List<string>();

            // AgentIdentity from Recipient.AgenticAppId
            var agentIdentity = activity.Recipient?.AgenticAppId;
            if (!string.IsNullOrEmpty(agentIdentity))
            {
                queryParams.Add($"AgentIdentity={Uri.EscapeDataString(agentIdentity)}");
            }

            // For AgenticUser role, add either AgentUserId or AgentUsername
            if (string.Equals(activity.Recipient?.Role, RoleTypes.AgenticUser, StringComparison.OrdinalIgnoreCase))
            {
                var agentUserId = activity.Recipient?.AgenticUserId;
                if (!string.IsNullOrEmpty(agentUserId))
                {
                    queryParams.Add($"AgentUserId={Uri.EscapeDataString(agentUserId)}");
                }
                else
                {
                    // Fall back to AgentUsername from Recipient.Id
                    var agentUsername = activity.Recipient?.Id;
                    if (!string.IsNullOrEmpty(agentUsername))
                    {
                        queryParams.Add($"AgentUsername={Uri.EscapeDataString(agentUsername)}");
                    }
                }
            }

            // Optional scope overrides
            var scopes = exchangeScopes ?? _settings.Scopes;
            if (scopes != null)
            {
                foreach (var scope in scopes)
                {
                    queryParams.Add($"optionsOverride.Scopes={Uri.EscapeDataString(scope)}");
                }
            }

            if (queryParams.Count > 0)
            {
                url += "?" + string.Join("&", queryParams);
            }

            return url;
        }

        private static string ExtractTokenFromResponse(string responseContent)
        {
            using var document = JsonDocument.Parse(responseContent);
            if (document.RootElement.TryGetProperty("authorizationHeader", out var authHeader))
            {
                var headerValue = authHeader.GetString();
                if (!string.IsNullOrEmpty(headerValue))
                {
                    // Strip the scheme prefix (e.g., "Bearer " or "PoP ")
                    var spaceIndex = headerValue.IndexOf(' ');
                    if (spaceIndex > 0)
                    {
                        return headerValue.Substring(spaceIndex + 1);
                    }

                    return headerValue;
                }
            }

            return null;
        }
    }
}
