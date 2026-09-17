// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed class GitHubDeviceFlowTokenClient : IGitHubDeviceFlowTokenClient
{
    private const int DefaultPollingIntervalSeconds = 5;
    private const string JsonMediaType = "application/json";
    private readonly HttpClient _httpClient;
    private readonly A2AClientAuthenticationOptions _options;
    private readonly TextWriter _output;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;

    public GitHubDeviceFlowTokenClient(
        HttpClient httpClient,
        A2AClientAuthenticationOptions options,
        TextWriter output,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _delayAsync = delayAsync ?? Task.Delay;
    }

    public async Task<string> AcquireTokenAsync(A2AAgentCardAuthentication authentication, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authentication);
        ValidateRequiredConfiguration();
        GitHubDeviceFlowAuthentication.Validate(authentication);

        GitHubDeviceAuthorizationResponse deviceAuthorization = await RequestDeviceAuthorizationAsync(authentication, cancellationToken).ConfigureAwait(false);
        await _output.WriteLineAsync(
                $"GitHub device sign-in: open {deviceAuthorization.VerificationUri} and enter code {deviceAuthorization.UserCode}")
            .ConfigureAwait(false);

        TimeSpan interval = TimeSpan.FromSeconds(deviceAuthorization.Interval ?? DefaultPollingIntervalSeconds);
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddSeconds(deviceAuthorization.ExpiresIn);
        int consecutiveTimeoutCount = 0;

        while (DateTimeOffset.UtcNow < expiresAt)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _delayAsync(GetPollingDelay(interval, consecutiveTimeoutCount), cancellationToken).ConfigureAwait(false);

            if (DateTimeOffset.UtcNow >= expiresAt)
            {
                break;
            }

            GitHubDeviceTokenResponse tokenResponse;
            try
            {
                tokenResponse = await RequestTokenAsync(authentication, deviceAuthorization.DeviceCode!, cancellationToken)
                    .ConfigureAwait(false);
                consecutiveTimeoutCount = 0;
            }
            catch (OperationCanceledException exception) when (IsTimeoutDuringPolling(exception, cancellationToken))
            {
                consecutiveTimeoutCount++;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
            {
                return tokenResponse.AccessToken;
            }

            string? error = string.IsNullOrWhiteSpace(tokenResponse.Error) ? null : tokenResponse.Error;
            switch (error)
            {
                case "authorization_pending":
                    continue;
                case "slow_down":
                    interval += TimeSpan.FromSeconds(5);
                    continue;
                case "expired_token":
                    throw new InvalidOperationException(CreateTokenErrorMessage(
                        "GitHub device authorization expired before completion.",
                        tokenResponse));
                case "access_denied":
                    throw new InvalidOperationException(CreateTokenErrorMessage(
                        "GitHub device authorization was denied by the user.",
                        tokenResponse));
                case null:
                    throw new InvalidOperationException("GitHub token endpoint response is missing access token.");
                default:
                    throw new InvalidOperationException(CreateTokenErrorMessage(
                        $"GitHub token endpoint returned error '{error}'.",
                        tokenResponse));
            }
        }

        throw new InvalidOperationException("GitHub device authorization expired before completion.");
    }

    private static TimeSpan GetPollingDelay(TimeSpan interval, int consecutiveTimeoutCount)
    {
        if (consecutiveTimeoutCount <= 0)
        {
            return interval;
        }

        double multiplier = Math.Pow(2, consecutiveTimeoutCount);
        return TimeSpan.FromMilliseconds(interval.TotalMilliseconds * multiplier);
    }

    private static bool IsTimeoutDuringPolling(OperationCanceledException exception, CancellationToken cancellationToken)
        => !cancellationToken.IsCancellationRequested
            && exception.InnerException is TimeoutException;

    private static string CreateTokenErrorMessage(string message, GitHubDeviceTokenResponse tokenResponse)
        => string.IsNullOrWhiteSpace(tokenResponse.ErrorDescription)
            ? message
            : $"{message} {tokenResponse.ErrorDescription}";

    private void ValidateRequiredConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.GitHubClientId))
        {
            throw new InvalidOperationException(
                $"Missing required A2A client authentication configuration value(s): {nameof(A2AClientAuthenticationOptions.GitHubClientId)}.");
        }
    }

    private async Task<GitHubDeviceAuthorizationResponse> RequestDeviceAuthorizationAsync(
        A2AAgentCardAuthentication authentication,
        CancellationToken cancellationToken)
    {
        using var request = CreatePostRequest(
            GitHubDeviceFlowAuthentication.GetRequiredDeviceAuthorizationEndpoint(authentication),
            [
                new("client_id", _options.GitHubClientId!),
                new("scope", string.Join(' ', authentication.Scopes)),
            ]);

        GitHubDeviceAuthorizationResponse response = await SendAsync<GitHubDeviceAuthorizationResponse>(
            request,
            endpointName: "device authorization endpoint",
            cancellationToken).ConfigureAwait(false);

        ValidateRequiredMember(response.DeviceCode, "device_code");
        ValidateRequiredMember(response.UserCode, "user_code");
        ValidateAbsoluteUri(response.VerificationUri, "verification_uri");
        ValidatePositiveNumber(response.ExpiresIn, "expires_in");
        ValidateOptionalPositiveNumber(response.Interval, "interval");
        return response;
    }

    private async Task<GitHubDeviceTokenResponse> RequestTokenAsync(
        A2AAgentCardAuthentication authentication,
        string deviceCode,
        CancellationToken cancellationToken)
    {
        using var request = CreatePostRequest(
            GitHubDeviceFlowAuthentication.GetRequiredTokenEndpoint(authentication),
            [
                new("client_id", _options.GitHubClientId!),
                new("device_code", deviceCode),
                new("grant_type", "urn:ietf:params:oauth:grant-type:device_code"),
            ]);

        return await SendAsync<GitHubDeviceTokenResponse>(
            request,
            endpointName: "token endpoint",
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> SendAsync<T>(
        HttpRequestMessage request,
        string endpointName,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException($"GitHub {endpointName} request failed.", exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"GitHub {endpointName} returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase ?? "Unknown"}).");
            }

            try
            {
                await using Stream content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                T? payload = await JsonSerializer.DeserializeAsync<T>(content, cancellationToken: cancellationToken).ConfigureAwait(false);
                return payload ?? throw new InvalidOperationException($"GitHub {endpointName} returned an empty JSON payload.");
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException($"GitHub {endpointName} returned malformed JSON.", exception);
            }
        }
    }

    private static HttpRequestMessage CreatePostRequest(Uri requestUri, IEnumerable<KeyValuePair<string, string>> formFields)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = new FormUrlEncodedContent(formFields),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonMediaType));
        return request;
    }

    private static void ValidateRequiredMember(string? value, string memberName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"GitHub device authorization endpoint response is missing required member '{memberName}'.");
        }
    }

    private static void ValidateAbsoluteUri(string? value, string memberName)
    {
        ValidateRequiredMember(value, memberName);

        if (!Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException(
                $"GitHub device authorization endpoint response member '{memberName}' must be an absolute URI.");
        }
    }

    private static void ValidatePositiveNumber(int value, string memberName)
    {
        if (value <= 0)
        {
            throw new InvalidOperationException(
                $"GitHub device authorization endpoint response is missing required member '{memberName}'.");
        }
    }

    private static void ValidateOptionalPositiveNumber(int? value, string memberName)
    {
        if (value.HasValue && value.Value <= 0)
        {
            throw new InvalidOperationException(
                $"GitHub device authorization endpoint response member '{memberName}' must be greater than zero.");
        }
    }
}
