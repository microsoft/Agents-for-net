// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Agents.Samples.A2AClient;

internal static class OAuthEndpointValidator
{
    public static Uri GetAdvertisedEndpoint(string? value, string endpointName)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? endpoint)
            || !endpoint.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The Agent Card {endpointName} must be an absolute HTTPS URI.");
        }

        return endpoint;
    }

    public static Uri GetTrustedEndpoint(
        string? value,
        OAuthCredentialProviderOptions provider,
        string endpointName)
    {
        Uri endpoint = GetAdvertisedEndpoint(value, endpointName);
        if (GetCommonTrustMatchSpecificity([endpoint], provider.AllowedAuthorities, provider.AllowedOrigins) is not null)
        {
            return endpoint;
        }

        throw new InvalidOperationException(
            $"The Agent Card {endpointName} origin '{endpoint.GetLeftPart(UriPartial.Authority)}' "
            + "is not trusted by the selected OAuth provider.");
    }

    public static int? GetCommonTrustMatchSpecificity(
        IReadOnlyList<Uri> endpoints,
        IReadOnlyList<Uri> allowedAuthorities,
        IReadOnlyList<Uri> allowedOrigins)
    {
        if (endpoints.Count == 0)
        {
            throw new InvalidOperationException("At least one advertised OAuth endpoint is required.");
        }

        int? bestAuthoritySpecificity = null;
        foreach (Uri allowedAuthority in allowedAuthorities)
        {
            if (!endpoints.All(endpoint => IsAuthorityMatch(endpoint, allowedAuthority)))
            {
                continue;
            }

            int specificity = GetAuthoritySpecificity(allowedAuthority);
            bestAuthoritySpecificity = bestAuthoritySpecificity is null
                ? specificity
                : Math.Max(bestAuthoritySpecificity.Value, specificity);
        }

        if (bestAuthoritySpecificity is not null)
        {
            return bestAuthoritySpecificity;
        }

        foreach (Uri allowedOrigin in allowedOrigins)
        {
            if (endpoints.All(endpoint => IsOriginMatch(endpoint, allowedOrigin)))
            {
                return 0;
            }
        }

        return null;
    }

    public static bool IsSupportedLoopbackRedirectUri(Uri? redirectUri)
        => redirectUri is not null
            && redirectUri.IsAbsoluteUri
            && redirectUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && redirectUri.IsLoopback
            && redirectUri.AbsolutePath.EndsWith("/", StringComparison.Ordinal);

    public static void EnsureSupportedLoopbackRedirectUri(Uri? redirectUri)
    {
        if (!IsSupportedLoopbackRedirectUri(redirectUri))
        {
            throw new InvalidOperationException(
                "Authorization Code requires an HTTP loopback RedirectUri whose path ends with '/'.");
        }
    }

    private static string NormalizePath(string path)
        => string.IsNullOrEmpty(path) || path == "/"
            ? string.Empty
            : path.TrimEnd('/');

    private static int GetAuthoritySpecificity(Uri allowedAuthority)
        => NormalizePath(allowedAuthority.AbsolutePath).Length == 0 ? 1 : 2;

    private static bool IsAuthorityMatch(Uri endpoint, Uri allowedAuthority)
        => IsOriginMatch(endpoint, allowedAuthority)
            && IsAuthorityPathMatch(endpoint, allowedAuthority);

    private static bool IsAuthorityPathMatch(Uri endpoint, Uri allowedAuthority)
    {
        string authorityPath = NormalizePath(allowedAuthority.AbsolutePath);
        if (authorityPath.Length == 0)
        {
            return true;
        }

        string endpointPath = NormalizePath(endpoint.AbsolutePath);
        return endpointPath.Equals(authorityPath, StringComparison.Ordinal)
            || endpointPath.StartsWith(authorityPath + "/", StringComparison.Ordinal);
    }

    private static bool IsOriginMatch(Uri endpoint, Uri allowedOrigin)
        => Uri.Compare(
            endpoint,
            allowedOrigin,
            UriComponents.SchemeAndServer,
            UriFormat.Unescaped,
            StringComparison.OrdinalIgnoreCase) == 0;
}
