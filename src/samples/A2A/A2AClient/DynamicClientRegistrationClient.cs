// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed class DynamicClientRegistrationClient : IDynamicClientRegistrationClient
{
    private const string AuthorizationCodeGrantType = "authorization_code";
    private const string NoneTokenEndpointAuthenticationMethod = "none";
    private const string ResponseTypeCode = "code";
    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
    };

    private readonly HttpClient _httpClient;

    public DynamicClientRegistrationClient()
        : this(new HttpClient(CreateInnerHandler()))
    {
    }

    internal DynamicClientRegistrationClient(HttpMessageHandler handler)
        : this(new HttpClient(handler ?? throw new ArgumentNullException(nameof(handler))))
    {
    }

    internal DynamicClientRegistrationClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    internal static HttpMessageHandler CreateInnerHandler()
        => new HttpClientHandler { AllowAutoRedirect = false };

    public async Task<OAuthClientRegistration> RegisterPublicClientAsync(
        string registrationId,
        Uri registrationEndpoint,
        Uri redirectUri,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registrationId);
        cancellationToken.ThrowIfCancellationRequested();

        Uri trustedRegistrationEndpoint = EnsureAbsoluteHttpsUri(registrationEndpoint, "registration endpoint");
        Uri trustedRedirectUri = EnsureAbsoluteUri(redirectUri, "redirect URI");

        using var request = new HttpRequestMessage(HttpMethod.Post, trustedRegistrationEndpoint)
        {
            Content = CreateContent(trustedRedirectUri),
        };

        using HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is < HttpStatusCode.OK or >= HttpStatusCode.MultipleChoices)
        {
            throw await CreateHttpFailureExceptionAsync(response, cancellationToken).ConfigureAwait(false);
        }

        RegistrationResponseDocument document = await DeserializeResponseAsync(
            response.Content,
            trustedRegistrationEndpoint,
            cancellationToken).ConfigureAwait(false);

        return ValidateResponse(registrationId, trustedRedirectUri, document);
    }

    private static HttpContent CreateContent(Uri redirectUri)
    {
        var request = new RegistrationRequestDocument
        {
            RedirectUris = [redirectUri.AbsoluteUri],
            GrantTypes = [AuthorizationCodeGrantType],
            ResponseTypes = [ResponseTypeCode],
            TokenEndpointAuthMethod = NoneTokenEndpointAuthenticationMethod,
        };

        string json = JsonSerializer.Serialize(request, s_jsonSerializerOptions);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static async Task<RegistrationResponseDocument> DeserializeResponseAsync(
        HttpContent content,
        Uri registrationEndpoint,
        CancellationToken cancellationToken)
    {
        await using Stream contentStream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            RegistrationResponseDocument? document = await JsonSerializer.DeserializeAsync<RegistrationResponseDocument>(
                contentStream,
                s_jsonSerializerOptions,
                cancellationToken).ConfigureAwait(false);
            if (document is null)
            {
                throw new InvalidOperationException(
                    $"Dynamic client registration endpoint '{registrationEndpoint.AbsoluteUri}' returned an empty response.");
            }

            return document;
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Dynamic client registration endpoint '{registrationEndpoint.AbsoluteUri}' returned invalid JSON.",
                exception);
        }
    }

    private static OAuthClientRegistration ValidateResponse(
        string registrationId,
        Uri redirectUri,
        RegistrationResponseDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.ClientId))
        {
            throw new InvalidOperationException(
                "Dynamic client registration response must include client_id.");
        }

        if (document.ClientSecret is not null)
        {
            throw new InvalidOperationException(
                "Dynamic client registration response must not include client_secret for a public client.");
        }

        if (document.TokenEndpointAuthMethod is not null
            && !document.TokenEndpointAuthMethod.Equals(NoneTokenEndpointAuthenticationMethod, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Dynamic client registration response token_endpoint_auth_method must be absent or 'none', "
                + $"but was '{document.TokenEndpointAuthMethod}'.");
        }

        if (document.RedirectUris is not null
            && !ContainsRedirectUri(document.RedirectUris, redirectUri))
        {
            throw new InvalidOperationException(
                $"Dynamic client registration response redirect_uris must include '{redirectUri.AbsoluteUri}'.");
        }

        return new OAuthClientRegistration(
            registrationId,
            [A2AOAuthFlowType.AuthorizationCode],
            document.ClientId,
            ClientSecret: null,
            RedirectUri: redirectUri,
            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.None,
            UsePkce: true);
    }

    private static bool ContainsRedirectUri(IReadOnlyList<string> redirectUris, Uri redirectUri)
    {
        foreach (string candidate in redirectUris)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (Uri.TryCreate(candidate, UriKind.Absolute, out Uri? parsed)
                && parsed.AbsoluteUri.Equals(redirectUri.AbsoluteUri, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<InvalidOperationException> CreateHttpFailureExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        string baseMessage =
            $"Dynamic client registration endpoint returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase ?? "Unknown"}).";
        (string? error, string? description) = await TryGetSafeErrorAsync(
            response.Content,
            cancellationToken).ConfigureAwait(false);
        if (error is null && description is null)
        {
            return new InvalidOperationException(baseMessage);
        }

        string detail = error is null
            ? description!
            : description is null
                ? error
                : $"{error}: {description}";
        return new InvalidOperationException($"{baseMessage} {detail}");
    }

    private static async Task<(string? Error, string? Description)> TryGetSafeErrorAsync(
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        if (content is null)
        {
            return (null, null);
        }

        try
        {
            string payload = await content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using JsonDocument json = JsonDocument.Parse(payload);
            JsonElement root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return (null, null);
            }

            string? error = root.TryGetProperty("error", out JsonElement errorElement)
                && errorElement.ValueKind == JsonValueKind.String
                    ? errorElement.GetString()
                    : null;
            string? description = root.TryGetProperty("error_description", out JsonElement descriptionElement)
                && descriptionElement.ValueKind == JsonValueKind.String
                    ? descriptionElement.GetString()
                    : null;
            return (error, description);
        }
        catch (JsonException)
        {
            return (null, null);
        }
        catch (InvalidOperationException)
        {
            return (null, null);
        }
    }

    private static Uri EnsureAbsoluteHttpsUri(Uri uri, string uriName)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!uri.IsAbsoluteUri
            || !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Dynamic client registration {uriName} must be an absolute HTTPS URI.");
        }

        return uri;
    }

    private static Uri EnsureAbsoluteUri(Uri uri, string uriName)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!uri.IsAbsoluteUri)
        {
            throw new InvalidOperationException(
                $"Dynamic client registration {uriName} must be an absolute URI.");
        }

        return uri;
    }

    private sealed class RegistrationRequestDocument
    {
        [JsonPropertyName("grant_types")]
        public required string[] GrantTypes { get; init; }

        [JsonPropertyName("redirect_uris")]
        public required string[] RedirectUris { get; init; }

        [JsonPropertyName("response_types")]
        public required string[] ResponseTypes { get; init; }

        [JsonPropertyName("token_endpoint_auth_method")]
        public required string TokenEndpointAuthMethod { get; init; }
    }

    private sealed class RegistrationResponseDocument
    {
        [JsonPropertyName("client_id")]
        public string? ClientId { get; init; }

        [JsonPropertyName("client_secret")]
        public string? ClientSecret { get; init; }

        [JsonPropertyName("redirect_uris")]
        public string[]? RedirectUris { get; init; }

        [JsonPropertyName("token_endpoint_auth_method")]
        public string? TokenEndpointAuthMethod { get; init; }
    }
}
