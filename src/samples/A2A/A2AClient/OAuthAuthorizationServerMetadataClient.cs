// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed class OAuthAuthorizationServerMetadataClient : IOAuthAuthorizationServerMetadataClient
{
    private const string AuthorizationCodeGrantType = "authorization_code";
    private const string OpenIdConfigurationPath = "/.well-known/openid-configuration";
    private const string OauthAuthorizationServerPath = "/.well-known/oauth-authorization-server";
    private const string S256CodeChallengeMethod = "S256";
    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
    };

    private readonly HttpClient _httpClient;

    public OAuthAuthorizationServerMetadataClient()
        : this(new HttpClient(CreateInnerHandler()))
    {
    }

    internal OAuthAuthorizationServerMetadataClient(HttpMessageHandler handler)
        : this(new HttpClient(handler ?? throw new ArgumentNullException(nameof(handler))))
    {
    }

    internal OAuthAuthorizationServerMetadataClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    internal static HttpMessageHandler CreateInnerHandler()
        => new HttpClientHandler { AllowAutoRedirect = false };

    internal static IReadOnlyList<Uri> GetMetadataCandidatesFromMetadataUrl(Uri metadataUrl)
        => DeduplicateMetadataCandidates(
        [
            EnsureAbsoluteHttpsUri(metadataUrl, "metadata URL"),
        ]);

    internal static IReadOnlyList<Uri> GetMetadataCandidatesFromOrigin(Uri endpoint)
    {
        Uri trustedEndpoint = EnsureAbsoluteHttpsUri(endpoint, "OAuth endpoint");
        string origin = trustedEndpoint.GetLeftPart(UriPartial.Authority);
        return DeduplicateMetadataCandidates(
        [
            new Uri(origin + OauthAuthorizationServerPath),
            new Uri(origin + OpenIdConfigurationPath),
        ]);
    }

    internal static IReadOnlyList<Uri> GetMetadataCandidatesFromIssuer(Uri issuerOrServerUrl)
    {
        Uri trustedIssuer = EnsureAbsoluteHttpsUri(issuerOrServerUrl, "issuer or server URL");
        string origin = trustedIssuer.GetLeftPart(UriPartial.Authority);
        string normalizedPath = NormalizePath(trustedIssuer.AbsolutePath);
        return DeduplicateMetadataCandidates(
        [
            new Uri(origin + OauthAuthorizationServerPath + normalizedPath),
            new Uri(origin + normalizedPath + OpenIdConfigurationPath),
        ]);
    }

    internal static IReadOnlyList<Uri> DeduplicateMetadataCandidates(IEnumerable<Uri> metadataCandidates)
    {
        ArgumentNullException.ThrowIfNull(metadataCandidates);

        var distinctCandidates = new List<Uri>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (Uri candidate in metadataCandidates)
        {
            Uri absoluteCandidate = EnsureAbsoluteHttpsUri(candidate, "metadata candidate");
            if (seen.Add(absoluteCandidate.AbsoluteUri))
            {
                distinctCandidates.Add(absoluteCandidate);
            }
        }

        return distinctCandidates;
    }

    public async Task<OAuthAuthorizationServerMetadata> DiscoverAsync(
        IReadOnlyList<Uri> metadataCandidates,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(metadataCandidates);

        IReadOnlyList<Uri> candidates = DeduplicateMetadataCandidates(metadataCandidates);
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("At least one HTTPS metadata discovery candidate is required.");
        }

        var attemptedCandidates = new List<string>(candidates.Count);
        foreach (Uri candidate in candidates)
        {
            attemptedCandidates.Add(candidate.AbsoluteUri);

            try
            {
                return await DiscoverFromCandidateAsync(candidate, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException)
            {
            }
        }

        throw new InvalidOperationException(
            $"Failed to discover OAuth authorization server metadata from: {string.Join(", ", attemptedCandidates)}.");
    }

    private async Task<OAuthAuthorizationServerMetadata> DiscoverFromCandidateAsync(
        Uri candidate,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, candidate);
        using HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is < HttpStatusCode.OK or >= HttpStatusCode.MultipleChoices)
        {
            throw new InvalidOperationException(
                $"Metadata candidate '{candidate.AbsoluteUri}' returned HTTP {(int)response.StatusCode}.");
        }

        AuthorizationServerMetadataDocument document = await DeserializeMetadataAsync(
            response.Content,
            candidate,
            cancellationToken).ConfigureAwait(false);

        return ValidateMetadata(candidate, document);
    }

    private static async Task<AuthorizationServerMetadataDocument> DeserializeMetadataAsync(
        HttpContent content,
        Uri candidate,
        CancellationToken cancellationToken)
    {
        await using Stream contentStream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            AuthorizationServerMetadataDocument? document = await JsonSerializer.DeserializeAsync<AuthorizationServerMetadataDocument>(
                contentStream,
                s_jsonSerializerOptions,
                cancellationToken).ConfigureAwait(false);
            if (document is null)
            {
                throw new InvalidOperationException(
                    $"Metadata candidate '{candidate.AbsoluteUri}' returned an empty response.");
            }

            return document;
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Metadata candidate '{candidate.AbsoluteUri}' returned invalid JSON.",
                exception);
        }
    }

    private static OAuthAuthorizationServerMetadata ValidateMetadata(
        Uri candidate,
        AuthorizationServerMetadataDocument document)
    {
        Uri issuer = EnsureAbsoluteHttpsUri(document.Issuer, "issuer");
        EnsureIssuerMatchesCandidate(candidate, issuer);

        Uri? authorizationEndpoint = document.AuthorizationEndpoint is null
            ? null
            : EnsureAbsoluteHttpsUri(document.AuthorizationEndpoint, "authorization endpoint");
        Uri tokenEndpoint = EnsureAbsoluteHttpsUri(document.TokenEndpoint, "token endpoint");
        Uri registrationEndpoint = EnsureAbsoluteHttpsUri(document.RegistrationEndpoint, "registration endpoint");
        IReadOnlyList<string> grantTypesSupported = NormalizeRequiredValues(
            document.GrantTypesSupported,
            "grant_types_supported");
        IReadOnlyList<string> codeChallengeMethodsSupported = NormalizeRequiredValues(
            document.CodeChallengeMethodsSupported,
            "code_challenge_methods_supported");

        if (!grantTypesSupported.Contains(AuthorizationCodeGrantType, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Metadata candidate '{candidate.AbsoluteUri}' does not support Authorization Code.");
        }

        if (!codeChallengeMethodsSupported.Contains(S256CodeChallengeMethod, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Metadata candidate '{candidate.AbsoluteUri}' does not support PKCE S256.");
        }

        return new OAuthAuthorizationServerMetadata(
            issuer,
            candidate,
            authorizationEndpoint,
            tokenEndpoint,
            registrationEndpoint,
            grantTypesSupported,
            codeChallengeMethodsSupported);
    }

    private static void EnsureIssuerMatchesCandidate(Uri candidate, Uri issuer)
    {
        if (!SharesOrigin(candidate, issuer))
        {
            throw new InvalidOperationException(
                $"Metadata candidate '{candidate.AbsoluteUri}' returned issuer '{issuer.AbsoluteUri}' on a different origin.");
        }

        Uri? derivedIssuer = TryDeriveIssuer(candidate);
        if (derivedIssuer is not null && !IsNormalizedIssuerMatch(issuer, derivedIssuer))
        {
            throw new InvalidOperationException(
                $"Metadata candidate '{candidate.AbsoluteUri}' returned issuer '{issuer.AbsoluteUri}' that does not match the well-known issuer path.");
        }
    }

    private static IReadOnlyList<string> NormalizeRequiredValues(
        IReadOnlyList<string>? values,
        string fieldName)
    {
        if (values is null)
        {
            throw new InvalidOperationException($"Metadata field '{fieldName}' is required.");
        }

        string[] normalizedValues = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (normalizedValues.Length == 0)
        {
            throw new InvalidOperationException($"Metadata field '{fieldName}' is required.");
        }

        return normalizedValues;
    }

    private static Uri EnsureAbsoluteHttpsUri(string? value, string uriName)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            throw new InvalidOperationException($"Metadata field '{uriName}' must be an absolute HTTPS URI.");
        }

        return EnsureAbsoluteHttpsUri(uri, uriName);
    }

    private static Uri EnsureAbsoluteHttpsUri(Uri uri, string uriName)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!uri.IsAbsoluteUri
            || !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Metadata field '{uriName}' must be an absolute HTTPS URI.");
        }

        return uri;
    }

    private static bool SharesOrigin(Uri left, Uri right)
        => Uri.Compare(
            left,
            right,
            UriComponents.SchemeAndServer,
            UriFormat.Unescaped,
            StringComparison.OrdinalIgnoreCase) == 0;

    private static Uri? TryDeriveIssuer(Uri candidate)
    {
        string candidatePath = candidate.AbsolutePath;
        if (candidatePath.Equals(OauthAuthorizationServerPath, StringComparison.Ordinal))
        {
            return BuildOriginUri(candidate, string.Empty);
        }

        string oauthPrefix = OauthAuthorizationServerPath + "/";
        if (candidatePath.StartsWith(oauthPrefix, StringComparison.Ordinal))
        {
            return BuildOriginUri(candidate, candidatePath[(oauthPrefix.Length - 1)..]);
        }

        if (candidatePath.Equals(OpenIdConfigurationPath, StringComparison.Ordinal))
        {
            return BuildOriginUri(candidate, string.Empty);
        }

        if (candidatePath.EndsWith(OpenIdConfigurationPath, StringComparison.Ordinal))
        {
            return BuildOriginUri(candidate, candidatePath[..^OpenIdConfigurationPath.Length]);
        }

        return null;
    }

    private static Uri BuildOriginUri(Uri source, string path)
    {
        string normalizedPath = NormalizePath(path);
        string origin = source.GetLeftPart(UriPartial.Authority);
        return normalizedPath.Length == 0
            ? new Uri(origin)
            : new Uri(origin + normalizedPath);
    }

    private static bool IsNormalizedIssuerMatch(Uri issuer, Uri expectedIssuer)
        => SharesOrigin(issuer, expectedIssuer)
            && NormalizePath(issuer.AbsolutePath).Equals(
                NormalizePath(expectedIssuer.AbsolutePath),
                StringComparison.Ordinal);

    private static string NormalizePath(string path)
        => string.IsNullOrEmpty(path) || path == "/"
            ? string.Empty
            : path.TrimEnd('/');

    private sealed class AuthorizationServerMetadataDocument
    {
        [JsonPropertyName("authorization_endpoint")]
        public string? AuthorizationEndpoint { get; init; }

        [JsonPropertyName("code_challenge_methods_supported")]
        public string[]? CodeChallengeMethodsSupported { get; init; }

        [JsonPropertyName("grant_types_supported")]
        public string[]? GrantTypesSupported { get; init; }

        [JsonPropertyName("issuer")]
        public string? Issuer { get; init; }

        [JsonPropertyName("registration_endpoint")]
        public string? RegistrationEndpoint { get; init; }

        [JsonPropertyName("token_endpoint")]
        public string? TokenEndpoint { get; init; }
    }
}
