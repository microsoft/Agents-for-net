// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Agents.Authentication.EntraAuthSidecar.Model;
using Microsoft.Agents.Core.Errors;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Authentication.EntraAuthSidecar
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
    /// <param name="requestTimeout">
    /// Per-attempt request timeout. Defaults to <see cref="DefaultTimeout"/> (30 seconds) when not specified.
    /// </param>
    /// <param name="retryCount">
    /// Number of retry attempts for transient failures (5xx, 408, 429, network/timeout). Defaults to
    /// <see cref="DefaultRetryCount"/> (3). A value of 0 disables retries.
    /// </param>
    /// <param name="retryBackoffBase">
    /// Base delay for the exponential retry backoff (base, base*2, base*4, ...). Defaults to 2 seconds.
    /// Primarily an override seam for tests.
    /// </param>
    internal class SidecarHttpClient(HttpClient httpClient, string baseUrl, ILogger logger = null, TimeSpan? requestTimeout = null, int retryCount = 3, TimeSpan? retryBackoffBase = null)
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

        /// <summary>
        /// Default number of retry attempts for transient sidecar failures.
        /// </summary>
        internal const int DefaultRetryCount = 3;

        private static readonly JsonSerializerOptions ProblemDetailsJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        private readonly string _baseUrl = (baseUrl ?? ResolveBaseUrl()).TrimEnd('/');
        private readonly ILogger _logger = logger ?? NullLogger.Instance;
        private readonly TimeSpan _requestTimeout = requestTimeout ?? DefaultTimeout;
        private readonly int _retryCount = retryCount < 0 ? 0 : retryCount;
        private readonly TimeSpan _retryBackoffBase = retryBackoffBase ?? TimeSpan.FromSeconds(2);

        /// <summary>
        /// Resolves the sidecar base URL from environment variable or default.
        /// </summary>
        public static string ResolveBaseUrl(string configuredUrl = null)
        {
            // Treat empty/whitespace values as unset so an exported-but-blank SIDECAR_URL (or a blank
            // configured value) falls back to the next source instead of resolving to an invalid URL.
            var envUrl = Environment.GetEnvironmentVariable("SIDECAR_URL");
            if (!string.IsNullOrWhiteSpace(envUrl))
            {
                return envUrl;
            }

            return !string.IsNullOrWhiteSpace(configuredUrl) ? configuredUrl : "http://localhost:5178";
        }

        /// <summary>
        /// Validates that the resolved sidecar base URL is safe to call. The host MUST be a loopback
        /// address (<c>localhost</c>, <c>127.0.0.0/8</c>, <c>::1</c>) or a private network address
        /// (RFC 1918 / RFC 4193 / link-local). A public/routable address is rejected to avoid sending
        /// agent credentials off-box (SSRF safety). This check applies regardless of how the URL was
        /// resolved (configuration or the <c>SIDECAR_URL</c> environment variable).
        /// </summary>
        /// <param name="resolvedUrl">The resolved sidecar base URL.</param>
        /// <param name="bypassLocalNetworkRestriction">
        /// <c>true</c> to skip the loopback/private-address safety check entirely. This is UNSAFE and
        /// must only be enabled for a carefully validated private-network configuration where the
        /// sidecar is reachable at a non-private address that the operator trusts.
        /// </param>
        /// <exception cref="InvalidOperationException">Thrown when the URL is malformed or points to a disallowed address.</exception>
        public static void ValidateBaseUrl(string resolvedUrl, bool bypassLocalNetworkRestriction)
        {
            // Always require a well-formed absolute URL; the bypass flag only relaxes the
            // loopback/private-address restriction, never basic URL validation.
            if (string.IsNullOrEmpty(resolvedUrl) || !Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var uri))
            {
                throw new InvalidOperationException(
                    $"The resolved sidecar base URL '{resolvedUrl}' is not a valid absolute URL.");
            }

            // Always restrict to http/https and reject embedded userinfo, regardless of the bypass
            // flag: this URL is used for HTTP calls that carry the agent's identity context, so
            // non-HTTP schemes (e.g. file://) and credentials-in-URL must never slip through.
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException(
                    $"The resolved sidecar base URL '{resolvedUrl}' must use the http or https scheme.");
            }

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                throw new InvalidOperationException(
                    $"The resolved sidecar base URL '{resolvedUrl}' must not contain userinfo (credentials).");
            }

            if (bypassLocalNetworkRestriction)
            {
                return;
            }

            if (IsLoopbackOrPrivateHost(uri))
            {
                return;
            }

            throw new InvalidOperationException(
                $"The resolved sidecar base URL '{resolvedUrl}' points to a non-loopback, non-private address. " +
                "The Entra ID Agent Container must be reachable only from within the agent's network boundary. " +
                "Set 'BypassLocalNetworkRestriction' to true in the connection configuration to override this " +
                "safety check (UNSAFE: only for carefully validated private-network configurations).");
        }

        private static bool IsLoopbackOrPrivateHost(Uri uri)
        {
            var host = uri.Host;

            if (uri.HostNameType == UriHostNameType.Dns)
            {
                // Only "localhost" (and subdomains thereof) is treated as a safe DNS host; any other
                // DNS name cannot be verified as private here and must be opted into via explicit config.
                return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                    || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
            }

            if (IPAddress.TryParse(host, out var ip))
            {
                return IsPrivateOrLoopback(ip);
            }

            return false;
        }

        private static bool IsPrivateOrLoopback(IPAddress ip)
        {
            if (IPAddress.IsLoopback(ip))
            {
                return true;
            }

            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                var b = ip.GetAddressBytes();

                // 10.0.0.0/8
                if (b[0] == 10)
                {
                    return true;
                }

                // 172.16.0.0/12
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                {
                    return true;
                }

                // 192.168.0.0/16
                if (b[0] == 192 && b[1] == 168)
                {
                    return true;
                }

                // 169.254.0.0/16 (link-local)
                if (b[0] == 169 && b[1] == 254)
                {
                    return true;
                }

                return false;
            }

            if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                // fc00::/7 (unique local) or fe80::/10 (link-local)
                return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || (ip.GetAddressBytes()[0] & 0xFE) == 0xFC;
            }

            return false;
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

            return await SendAndParseAsync(() => new HttpRequestMessage(HttpMethod.Get, url), cancellationToken).ConfigureAwait(false);
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

            List<string> queryParams = [];

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
                    // Callers may pass null/empty scope entries; skip them so EscapeDataString never
                    // throws on null and the query string isn't padded with empty scope params.
                    if (string.IsNullOrWhiteSpace(scope))
                    {
                        continue;
                    }

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

        private async Task<SidecarTokenResult> SendAndParseAsync(Func<HttpRequestMessage> requestFactory, CancellationToken cancellationToken)
        {
            var maxAttempts = _retryCount + 1;

            for (var attempt = 1; ; attempt++)
            {
                using var request = requestFactory();
                using var timeoutCts = new CancellationTokenSource(_requestTimeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                // Log only the request path, never the full URI: the query string carries the agentic
                // user's UPN, object id, tenant, and agent client id (PII) which must not reach
                // default-enabled Warning/Error logs.
                var requestPath = request.RequestUri?.GetLeftPart(UriPartial.Path);

                _logger.LogDebug("Sidecar request (attempt {Attempt}/{MaxAttempts}): {Method} {Url}", attempt, maxAttempts, request.Method, requestPath);

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.SendAsync(request, linkedCts.Token).ConfigureAwait(false);
                }
                catch (Exception ex) when (IsTransientException(ex, cancellationToken))
                {
                    if (attempt >= maxAttempts)
                    {
                        _logger.LogError(ex, "Sidecar request to {Url} failed after {Attempts} attempt(s).", requestPath, attempt);
                        throw new ErrorResponseException(
                            $"Sidecar request failed after {attempt} attempt(s): {ex.Message}", ex)
                        {
                            StatusCode = 0
                        };
                    }

                    _logger.LogWarning(ex, "Sidecar request to {Url} failed (attempt {Attempt}/{MaxAttempts}); retrying.", requestPath, attempt, maxAttempts);
                    await DelayBeforeRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                try
                {
                    if (!response.IsSuccessStatusCode)
                    {
#if NETSTANDARD
                        var errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#else
                        var errorContent = await response.Content.ReadAsStringAsync(linkedCts.Token).ConfigureAwait(false);
#endif
                        if (IsTransientStatus(response.StatusCode) && attempt < maxAttempts)
                        {
                            _logger.LogWarning(
                                "Sidecar returned transient status {StatusCode} from {Url} (attempt {Attempt}/{MaxAttempts}); retrying.",
                                (int)response.StatusCode, requestPath, attempt, maxAttempts);
                            await DelayBeforeRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
                            continue;
                        }

                        var problemDetails = TryParseProblemDetails(errorContent);

                        _logger.LogError(
                            "Sidecar returned error. Status: {StatusCode}, URL: {Url}, ProblemTitle: {Title}, Detail: {Detail}, Body: {Body}",
                            (int)response.StatusCode,
                            requestPath,
                            problemDetails?.Title ?? "(none)",
                            problemDetails?.Detail ?? "(none)",
                            errorContent?.Length > 2000 ? errorContent.Substring(0, 2000) : errorContent);

                        var title = problemDetails?.Title ?? $"Sidecar returned {(int)response.StatusCode}";
                        var message = string.IsNullOrEmpty(problemDetails?.Detail)
                            ? title
                            : $"{title}: {problemDetails.Detail}";

                        throw new ErrorResponseException(message)
                        {
                            StatusCode = (int)response.StatusCode
                        };
                    }

#if NETSTANDARD
                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#else
                    var content = await response.Content.ReadAsStringAsync(linkedCts.Token).ConfigureAwait(false);
#endif

                    _logger.LogDebug("Sidecar response OK from {Url}. Response length: {Length}", requestPath, content?.Length ?? 0);

                    var result = ParseTokenResponse(content);
                    _logger.LogDebug(
                        "Sidecar token acquired. Scheme: {Scheme}, TokenLength: {TokenLength}",
                        result.Scheme,
                        result.Token?.Length ?? 0);

                    return result;
                }
                finally
                {
                    response.Dispose();
                }
            }
        }

        private static bool IsTransientStatus(HttpStatusCode statusCode)
        {
            return statusCode == HttpStatusCode.RequestTimeout          // 408
                || (int)statusCode == 429                               // TooManyRequests
                || (int)statusCode >= 500;                              // server errors
        }

        private static bool IsTransientException(Exception ex, CancellationToken callerToken)
        {
            // A cancellation requested by the caller is not transient — propagate it.
            if (callerToken.IsCancellationRequested)
            {
                return false;
            }

            // Network failures and per-attempt timeouts (surfaced as Task/OperationCanceledException
            // when the caller did not request cancellation) are retryable.
            return ex is HttpRequestException || ex is OperationCanceledException;
        }

        private async Task DelayBeforeRetryAsync(int attempt, CancellationToken cancellationToken)
        {
            // Exponential backoff: base, base*2, base*4, ...
            var multiplier = Math.Pow(2, attempt - 1);
            var delay = TimeSpan.FromMilliseconds(_retryBackoffBase.TotalMilliseconds * multiplier);
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
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
                                headerValue.Substring(0, spaceIndex),
                                headerValue.Substring(spaceIndex + 1));
                        }

                        return new SidecarTokenResult("Bearer", headerValue);
                    }
                }
            }
            catch (JsonException jsonEx)
            {
                throw new ErrorResponseException("Sidecar returned an unparsable response body.", jsonEx)
                {
                    StatusCode = 0
                };
            }

            throw new ErrorResponseException("Sidecar response missing authorizationHeader field")
            {
                StatusCode = 0
            };
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
