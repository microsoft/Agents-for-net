// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

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
    /// Reusable HTTP client for communicating with the Microsoft Entra SDK for Agent ID sidecar.
    /// Handles URL construction, query parameter building, response parsing, and error handling.
    /// </summary>
    /// <remarks>
    /// Creates a new <see cref="SidecarHttpClient"/> instance.
    /// </remarks>
    /// <param name="httpClient">The underlying HTTP client.</param>
    /// <param name="baseUrl">
    /// The sidecar base URL. Resolution order: SIDECAR_URL env var → explicit config → "http://localhost:5178".
    /// </param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    internal class SidecarHttpClient(HttpClient httpClient, string baseUrl, ILogger logger = null)
    {
        /// <summary>
        /// The <see cref="IHttpClientFactory"/> logical client name used for the sidecar client.
        /// </summary>
        internal const string HttpClientName = "EntraSidecarClient";

        /// <summary>
        /// Default request timeout applied to sidecar clients that are not created through the
        /// configured named <see cref="IHttpClientFactory"/> client.
        /// </summary>
        internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

        private static readonly JsonSerializerOptions ProblemDetailsJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        private readonly string _baseUrl = (baseUrl ?? ResolveBaseUrl()).TrimEnd('/');
        private readonly ILogger _logger = logger ?? NullLogger.Instance;

        /// <summary>
        /// Resolves the sidecar base URL from environment variable or default.
        /// </summary>
        public static string ResolveBaseUrl(string configuredUrl = null)
        {
            return Environment.GetEnvironmentVariable("SIDECAR_URL")
                ?? configuredUrl
                ?? "http://localhost:5178";
        }

        /// <summary>
        /// Calls GET /AuthorizationHeaderUnauthenticated/{serviceName} with the specified options.
        /// Used for app-only and autonomous agent token acquisition.
        /// </summary>
        public async Task<SidecarTokenResult> GetAuthorizationHeaderUnauthenticatedAsync(
            string serviceName,
            SidecarRequestOptions options = null,
            CancellationToken cancellationToken = default)
        {
            var url = BuildUrl($"/AuthorizationHeaderUnauthenticated/{Uri.EscapeDataString(serviceName)}", options);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            return await SendAndParseAsync(request, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Checks sidecar health via GET /healthz.
        /// </summary>
        public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var response = await _httpClient.GetAsync($"{_baseUrl}/healthz", cancellationToken).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private string BuildUrl(string path, SidecarRequestOptions options)
        {
            var url = $"{_baseUrl}{path}";

            if (options == null)
            {
                return url;
            }

            var queryParams = new List<string>();

            // AgentUsername (UPN) and AgentUserId (object id) are mutually exclusive per the sidecar
            // contract. Enforce the invariant centrally rather than relying on each caller.
            if (!string.IsNullOrEmpty(options.AgentUsername) && !string.IsNullOrEmpty(options.AgentUserId))
            {
                throw new InvalidOperationException(
                    "AgentUsername and AgentUserId are mutually exclusive; set only one on SidecarRequestOptions.");
            }

            // Agent identity context parameters
            if (!string.IsNullOrEmpty(options.AgentIdentity))
            {
                queryParams.Add($"AgentIdentity={Uri.EscapeDataString(options.AgentIdentity)}");
            }

            if (!string.IsNullOrEmpty(options.AgentUsername))
            {
                queryParams.Add($"AgentUsername={Uri.EscapeDataString(options.AgentUsername)}");
            }

            if (!string.IsNullOrEmpty(options.AgentUserId))
            {
                queryParams.Add($"AgentUserId={Uri.EscapeDataString(options.AgentUserId)}");
            }

            // Options overrides
            if (options.Scopes != null)
            {
                foreach (var scope in options.Scopes)
                {
                    queryParams.Add($"optionsOverride.Scopes={Uri.EscapeDataString(scope)}");
                }
            }

            if (options.RequestAppToken == true)
            {
                queryParams.Add("optionsOverride.RequestAppToken=true");
            }

            if (!string.IsNullOrEmpty(options.Tenant))
            {
                queryParams.Add($"optionsOverride.AcquireTokenOptions.Tenant={Uri.EscapeDataString(options.Tenant)}");
            }

            if (options.ForceRefresh == true)
            {
                queryParams.Add("optionsOverride.AcquireTokenOptions.ForceRefresh=true");
            }

            if (queryParams.Count > 0)
            {
                url += "?" + string.Join("&", queryParams);
            }

            return url;
        }

        private async Task<SidecarTokenResult> SendAndParseAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Sidecar request: {Method} {Url}", request.Method, request.RequestUri);

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
#if NETSTANDARD
                var errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#else
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#endif
                var problemDetails = TryParseProblemDetails(errorContent);

                _logger.LogError(
                    "Sidecar returned error. Status: {StatusCode}, URL: {Url}, ProblemTitle: {Title}, Detail: {Detail}, Body: {Body}",
                    (int)response.StatusCode,
                    request.RequestUri,
                    problemDetails?.Title ?? "(none)",
                    problemDetails?.Detail ?? "(none)",
                    errorContent?.Length > 2000 ? errorContent[..2000] : errorContent);

                throw new SidecarRequestException(
                    (int)response.StatusCode,
                    problemDetails?.Title ?? $"Sidecar returned {(int)response.StatusCode}",
                    problemDetails,
                    errorContent);
            }

#if NETSTANDARD
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#else
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#endif

            _logger.LogDebug("Sidecar response OK from {Url}. Response length: {Length}", request.RequestUri, content?.Length ?? 0);

            var result = ParseTokenResponse(content);
            _logger.LogDebug(
                "Sidecar token acquired. Scheme: {Scheme}, TokenLength: {TokenLength}",
                result.Scheme,
                result.Token?.Length ?? 0);

            return result;
        }

        private static SidecarTokenResult ParseTokenResponse(string responseContent)
        {
            try
            {
                using var document = JsonDocument.Parse(responseContent);
                if (document.RootElement.TryGetProperty("authorizationHeader", out var authHeader)
                    && authHeader.ValueKind == JsonValueKind.String)
                {
                    var headerValue = authHeader.GetString();
                    if (!string.IsNullOrEmpty(headerValue))
                    {
                        var spaceIndex = headerValue.IndexOf(' ');
                        if (spaceIndex > 0)
                        {
                            return new SidecarTokenResult(
                                headerValue[..spaceIndex],
                                headerValue[(spaceIndex + 1)..]);
                        }

                        return new SidecarTokenResult("Bearer", headerValue);
                    }
                }
            }
            catch (JsonException)
            {
                throw new SidecarRequestException(0, "Sidecar returned an unparsable response body.", null, responseContent);
            }

            throw new SidecarRequestException(0, "Sidecar response missing authorizationHeader field", null, responseContent);
        }

        private static SidecarProblemDetails TryParseProblemDetails(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<SidecarProblemDetails>(content, ProblemDetailsJsonOptions);
            }
            catch
            {
                return null;
            }
        }
    }
}
