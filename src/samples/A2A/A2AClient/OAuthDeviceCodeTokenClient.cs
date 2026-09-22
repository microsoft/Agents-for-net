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

internal sealed class OAuthDeviceCodeTokenClient
{
    private const int DefaultPollingIntervalSeconds = 5;
    private readonly HttpClient _httpClient;
    private readonly TextWriter _output;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;

    public OAuthDeviceCodeTokenClient(
        HttpClient httpClient,
        TextWriter output,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _delayAsync = delayAsync ?? Task.Delay;
    }

    public async Task<OAuthAccessToken> AcquireTokenAsync(
        OAuthCredentialBinding binding,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(binding);

        OAuthClientRegistration registration = binding.Registration;
        if (binding.FlowType != A2AOAuthFlowType.DeviceCode)
        {
            throw new InvalidOperationException("Device Code token acquisition requires a Device Code OAuth flow.");
        }

        ValidateClientId(registration);
        Uri deviceEndpoint = binding.DeviceAuthorizationEndpoint
            ?? throw new InvalidOperationException(
                "Device Code token acquisition requires a device authorization endpoint.");
        Uri tokenEndpoint = binding.TokenEndpoint;

        OAuthDeviceAuthorizationResponse authorization = await SendAsync<OAuthDeviceAuthorizationResponse>(
            CreatePostRequest(
                deviceEndpoint,
                registration,
                [
                    new("client_id", registration.ClientId),
                    new("scope", string.Join(' ', binding.EffectiveScopes)),
                ],
                authenticateClient: false),
            "device authorization endpoint",
            cancellationToken).ConfigureAwait(false);

        ValidateRequiredMember(authorization.DeviceCode, "device_code");
        ValidateRequiredMember(authorization.UserCode, "user_code");
        ValidateAbsoluteUri(authorization.VerificationUri, "verification_uri");
        ValidatePositiveNumber(authorization.ExpiresIn, "expires_in");
        ValidateOptionalPositiveNumber(authorization.Interval, "interval");

        string prompt = !string.IsNullOrWhiteSpace(authorization.Message)
            ? authorization.Message
            : $"OAuth device sign-in: open {authorization.VerificationUri} and enter code {authorization.UserCode}";
        await _output.WriteLineAsync(prompt).ConfigureAwait(false);

        TimeSpan interval = TimeSpan.FromSeconds(authorization.Interval ?? DefaultPollingIntervalSeconds);
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddSeconds(authorization.ExpiresIn);
        int consecutiveTimeoutCount = 0;

        while (DateTimeOffset.UtcNow < expiresAt)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _delayAsync(GetPollingDelay(interval, consecutiveTimeoutCount), cancellationToken).ConfigureAwait(false);
            if (DateTimeOffset.UtcNow >= expiresAt)
            {
                break;
            }

            OAuthTokenResponse tokenResponse;
            try
            {
                tokenResponse = await SendAsync<OAuthTokenResponse>(
                    CreatePostRequest(
                        tokenEndpoint,
                        registration,
                        [
                            new("client_id", registration.ClientId),
                            new("device_code", authorization.DeviceCode!),
                            new("grant_type", "urn:ietf:params:oauth:grant-type:device_code"),
                        ],
                        authenticateClient: true),
                    "token endpoint",
                    cancellationToken,
                    allowOAuthErrorResponse: true).ConfigureAwait(false);
                consecutiveTimeoutCount = 0;
            }
            catch (OperationCanceledException exception) when (IsTimeoutDuringPolling(exception, cancellationToken))
            {
                consecutiveTimeoutCount++;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
            {
                ValidateOptionalPositiveNumber(tokenResponse.ExpiresIn, "expires_in");
                return new OAuthAccessToken(
                    tokenResponse.AccessToken,
                    tokenResponse.ExpiresIn is int expiresIn ? TimeSpan.FromSeconds(expiresIn) : null,
                    tokenResponse.RefreshToken);
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
                        "OAuth device authorization expired before completion.",
                        tokenResponse));
                case "access_denied":
                    throw new InvalidOperationException(CreateTokenErrorMessage(
                        "OAuth device authorization was denied by the user.",
                        tokenResponse));
                case null:
                    throw new InvalidOperationException("OAuth token endpoint response is missing access token.");
                default:
                    throw new InvalidOperationException(CreateTokenErrorMessage(
                        $"OAuth token endpoint returned error '{error}'.",
                        tokenResponse));
            }
        }

        throw new InvalidOperationException("OAuth device authorization expired before completion.");
    }

    private static TimeSpan GetPollingDelay(TimeSpan interval, int consecutiveTimeoutCount)
        => consecutiveTimeoutCount <= 0
            ? interval
            : TimeSpan.FromMilliseconds(interval.TotalMilliseconds * Math.Pow(2, consecutiveTimeoutCount));

    private static bool IsTimeoutDuringPolling(
        OperationCanceledException exception,
        CancellationToken cancellationToken)
        => !cancellationToken.IsCancellationRequested
            && exception.InnerException is TimeoutException;

    private static string CreateTokenErrorMessage(string message, OAuthTokenResponse response)
        => string.IsNullOrWhiteSpace(response.ErrorDescription)
            ? message
            : $"{message} {response.ErrorDescription}";

    private static HttpRequestMessage CreatePostRequest(
        Uri requestUri,
        OAuthClientRegistration registration,
        IEnumerable<KeyValuePair<string, string>> formFields,
        bool authenticateClient)
    {
        var fields = new List<KeyValuePair<string, string>>(formFields);
        var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        if (authenticateClient)
        {
            ApplyClientAuthentication(request, fields, registration);
        }

        request.Content = new FormUrlEncodedContent(fields);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static void ApplyClientAuthentication(
        HttpRequestMessage request,
        List<KeyValuePair<string, string>> fields,
        OAuthClientRegistration registration)
    {
        switch (registration.TokenEndpointAuthenticationMethod)
        {
            case OAuthTokenEndpointAuthenticationMethod.None:
                break;
            case OAuthTokenEndpointAuthenticationMethod.ClientSecretPost:
                ValidateClientSecret(registration);
                fields.Add(new("client_secret", registration.ClientSecret!));
                break;
            case OAuthTokenEndpointAuthenticationMethod.ClientSecretBasic:
                ValidateClientSecret(registration);
                string credentials = Convert.ToBase64String(
                    System.Text.Encoding.UTF8.GetBytes($"{registration.ClientId}:{registration.ClientSecret}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(registration));
        }
    }

    private async Task<T> SendAsync<T>(
        HttpRequestMessage request,
        string endpointName,
        CancellationToken cancellationToken,
        bool allowOAuthErrorResponse = false)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException($"OAuth {endpointName} request failed.", exception);
        }

        using (response)
        {
            try
            {
                await using System.IO.Stream content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                T? payload = await JsonSerializer.DeserializeAsync<T>(content, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (payload is null)
                {
                    throw new InvalidOperationException($"OAuth {endpointName} returned an empty JSON payload.");
                }

                if (!response.IsSuccessStatusCode
                    && !(allowOAuthErrorResponse
                        && payload is OAuthTokenResponse { Error: { Length: > 0 } }))
                {
                    string providerDetails = payload is OAuthDeviceAuthorizationResponse
                    {
                        Error: { Length: > 0 } error,
                        ErrorDescription: var description,
                    }
                        ? $" OAuth error '{error}'.{(string.IsNullOrWhiteSpace(description) ? string.Empty : $" {description}")}"
                        : string.Empty;
                    throw new InvalidOperationException(
                        $"OAuth {endpointName} returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase ?? "Unknown"}).{providerDetails}");
                }

                return payload;
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException($"OAuth {endpointName} returned malformed JSON.", exception);
            }
        }
    }

    private static void ValidateClientId(OAuthClientRegistration registration)
    {
        if (string.IsNullOrWhiteSpace(registration.ClientId))
        {
            throw new InvalidOperationException("The selected OAuth client registration requires ClientId.");
        }

        if (registration.ClientId.Trim().Trim('0', '-').Length == 0)
        {
            throw new InvalidOperationException(
                "The selected OAuth client registration ClientId is still a placeholder. Configure a registered OAuth client.");
        }
    }

    private static void ValidateClientSecret(OAuthClientRegistration registration)
    {
        if (string.IsNullOrWhiteSpace(registration.ClientSecret))
        {
            throw new InvalidOperationException(
                $"The selected OAuth client registration requires ClientSecret for {registration.TokenEndpointAuthenticationMethod}.");
        }
    }

    private static void ValidateRequiredMember(string? value, string memberName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"OAuth device authorization endpoint response is missing required member '{memberName}'.");
        }
    }

    private static void ValidateAbsoluteUri(string? value, string memberName)
    {
        ValidateRequiredMember(value, memberName);
        if (!Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException(
                $"OAuth device authorization endpoint response member '{memberName}' must be an absolute URI.");
        }
    }

    private static void ValidatePositiveNumber(int value, string memberName)
    {
        if (value <= 0)
        {
            throw new InvalidOperationException(
                $"OAuth device authorization endpoint response is missing required member '{memberName}'.");
        }
    }

    private static void ValidateOptionalPositiveNumber(int? value, string memberName)
    {
        if (value.HasValue && value.Value <= 0)
        {
            throw new InvalidOperationException(
                $"OAuth endpoint response member '{memberName}' must be greater than zero.");
        }
    }
}
