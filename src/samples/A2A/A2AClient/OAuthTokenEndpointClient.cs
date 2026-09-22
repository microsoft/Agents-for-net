// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Samples.A2AClient.OAuth.Registration;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed class OAuthTokenEndpointClient(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    public async Task<OAuthAccessToken> RequestTokenAsync(
        Uri tokenEndpoint,
        OAuthClientRegistration registration,
        IEnumerable<KeyValuePair<string, string>> tokenFields,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tokenEndpoint);
        ArgumentNullException.ThrowIfNull(registration);
        OAuthClientRegistrationValidator.EnsureUsableClientId(registration);

        var fields = new List<KeyValuePair<string, string>>(tokenFields);
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
        ApplyClientAuthentication(request, fields, registration);
        request.Content = new FormUrlEncodedContent(fields);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException("OAuth token endpoint request failed.", exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"OAuth token endpoint returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase ?? "Unknown"}).");
            }

            OAuthTokenResponse? tokenResponse;
            try
            {
                await using System.IO.Stream content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                tokenResponse = await JsonSerializer
                    .DeserializeAsync<OAuthTokenResponse>(content, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException("OAuth token endpoint returned malformed JSON.", exception);
            }

            if (tokenResponse is null)
            {
                throw new InvalidOperationException("OAuth token endpoint returned an empty JSON payload.");
            }

            if (string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
            {
                string error = string.IsNullOrWhiteSpace(tokenResponse.Error)
                    ? "missing access token"
                    : $"error '{tokenResponse.Error}'";
                string description = string.IsNullOrWhiteSpace(tokenResponse.ErrorDescription)
                    ? string.Empty
                    : $" {tokenResponse.ErrorDescription}";
                throw new InvalidOperationException($"OAuth token endpoint returned {error}.{description}");
            }

            if (tokenResponse.ExpiresIn.HasValue && tokenResponse.ExpiresIn.Value <= 0)
            {
                throw new InvalidOperationException(
                    "OAuth token endpoint response member 'expires_in' must be greater than zero.");
            }

            return new OAuthAccessToken(
                tokenResponse.AccessToken,
                tokenResponse.ExpiresIn is int expiresIn ? TimeSpan.FromSeconds(expiresIn) : null,
                tokenResponse.RefreshToken);
        }
    }

    private static void ApplyClientAuthentication(
        HttpRequestMessage request,
        List<KeyValuePair<string, string>> fields,
        OAuthClientRegistration registration)
    {
        fields.Add(new("client_id", registration.ClientId));

        switch (registration.TokenEndpointAuthenticationMethod)
        {
            case OAuthTokenEndpointAuthenticationMethod.None:
                return;
            case OAuthTokenEndpointAuthenticationMethod.ClientSecretPost:
                ValidateClientSecret(registration);
                fields.Add(new("client_secret", registration.ClientSecret!));
                return;
            case OAuthTokenEndpointAuthenticationMethod.ClientSecretBasic:
                ValidateClientSecret(registration);
                string credentials = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{registration.ClientId}:{registration.ClientSecret}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(registration));
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
}
