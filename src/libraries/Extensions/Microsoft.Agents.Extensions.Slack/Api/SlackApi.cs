// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Slack.Api;

/// <summary>
/// Provides methods for calling the Slack Web API using HTTP requests. This class enables integration with Slack by
/// sending API method calls and handling responses.    
/// </summary>
/// <remarks>Instances of this class use an injected IHttpClientFactory to create named HTTP clients for making requests
/// to Slack. The class is intended for use in applications that need to interact with Slack's API endpoints, such as
/// sending messages, retrieving user information, or managing channels.</remarks>
public class SlackApi
{
    private const string SlackApiBase = "https://slack.com/api";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Action? _onCallAsync;
    private readonly SlackRateLimiter _rateLimiter;
    internal TimeProvider TimeProvider => _rateLimiter.TimeProvider;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public SlackApi(IHttpClientFactory httpClientFactory) : this(httpClientFactory, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Slack.Api.SlackApi"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The factory used to create named HTTP clients for Slack requests.</param>
    /// <param name="onCallAsync">
    /// An optional callback invoked on every <see cref="Microsoft.Agents.Extensions.Slack.Api.SlackApi.CallAsync(System.String, System.Object, System.String, System.Threading.CancellationToken)"/> before the request is sent. This is used to
    /// notify the turn that a message is being delivered out-of-band (bypassing the send pipeline), for example to
    /// reset the per-turn typing timer interval so the typing indicator timing stays consistent.
    /// </param>
    public SlackApi(IHttpClientFactory httpClientFactory, Action? onCallAsync)
        : this(httpClientFactory, onCallAsync, SlackRateLimiter.Shared)
    {
    }

    internal SlackApi(IHttpClientFactory httpClientFactory, Action? onCallAsync, SlackRateLimiter rateLimiter)
    {
        AssertionHelpers.ThrowIfNull(httpClientFactory, nameof(httpClientFactory));
        _httpClientFactory = httpClientFactory;
        _onCallAsync = onCallAsync;
        _rateLimiter = rateLimiter;
    }

    /// <summary>
    /// Invokes a Slack Web API method asynchronously using the specified method name, options, and authentication
    /// token.
    /// </summary>
    /// <remarks>
    /// Uses the named HTTP client "SlackApi". Explicit rate-limit responses are retried up to three
    /// attempts, honoring Retry-After. Other API failures throw
    /// <see cref="Microsoft.Agents.Extensions.Slack.Api.SlackResponseException"/> and are not automatically retried.
    /// </remarks>
    /// <param name="method">The name of the Slack API method to call. Cannot be null, empty, or whitespace.</param>
    /// <param name="options">An object containing parameters to include in the API request body. May be null if no parameters are required.</param>
    /// <param name="token">The OAuth access token used to authenticate the request. If empty, the request may fail unless the API method
    /// allows unauthenticated access.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a SlackResponse object with the API
    /// response data.</returns>
    public Task<SlackResponse> CallAsync(string method, object? options = null, string token = "", CancellationToken cancellationToken = default)
        => CallCoreAsync(method, options, token, GetTokenScope(token), false, cancellationToken);

    internal Task<SlackResponse> CallStreamingAsync(
        string method, object options, string token, string scope, CancellationToken cancellationToken)
        => CallCoreAsync(method, options, token, scope ?? GetTokenScope(token), true, cancellationToken);

    private async Task<SlackResponse> CallCoreAsync(
        string method, object options, string token, string scope, bool pace, CancellationToken cancellationToken)
    {
        AssertionHelpers.ThrowIfNullOrWhiteSpace(method, nameof(method));
        var json = options is string str ? str : JsonSerializer.Serialize(options ?? new { }, JsonOptions);
        var key = scope + ":" + method;
        using var httpClient = _httpClientFactory.CreateClient(nameof(SlackApi));
        for (var attempt = 0; ; attempt++)
        {
            await _rateLimiter.WaitAsync(key, pace, cancellationToken).ConfigureAwait(false);
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{SlackApiBase}/{method}")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            _onCallAsync?.Invoke();
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
#if NETSTANDARD
            var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#else
            var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#endif
            cancellationToken.ThrowIfCancellationRequested();
            SlackResponse data = null;
            try
            {
                data = JsonSerializer.Deserialize<SlackResponse>(text);
            }
            catch (JsonException)
            {
            }
            if (response.IsSuccessStatusCode && data?.ok == true)
            {
                return data;
            }

            var throttled = (int)response.StatusCode == 429
                || data?.error is "ratelimited" or "rate_limited";
            var retryAfter = response.Headers.RetryAfter?.Delta
                ?? (response.Headers.RetryAfter?.Date - TimeProvider.GetUtcNow());
            if (throttled)
            {
                retryAfter ??= TimeSpan.FromSeconds(1);
                if (retryAfter < TimeSpan.Zero)
                {
                    retryAfter = TimeSpan.Zero;
                }
                _rateLimiter.Postpone(key, retryAfter.Value);
            }
            if (throttled && attempt < 2)
            {
                continue;
            }
            throw new SlackResponseException(
                method, response.StatusCode, data?.error ?? (throttled ? "ratelimited" : "invalid_response"),
                retryAfter, text);
        }
    }

    private static string GetTokenScope(string token)
    {
#if NET8_0_OR_GREATER
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token ?? "")));
#else
        using var sha = SHA256.Create();
        return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(token ?? "")));
#endif
    }
}
