// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Samples.A2AClient;

internal static class OAuthEndpointValidator
{
    public static Uri GetTrustedEndpoint(
        string? value,
        OAuthCredentialProviderOptions provider,
        string endpointName)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? endpoint)
            || !endpoint.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The Agent Card {endpointName} must be an absolute HTTPS URI.");
        }

        foreach (Uri allowedOrigin in provider.AllowedOrigins)
        {
            if (Uri.Compare(
                    endpoint,
                    allowedOrigin,
                    UriComponents.SchemeAndServer,
                    UriFormat.Unescaped,
                    StringComparison.OrdinalIgnoreCase) == 0)
            {
                return endpoint;
            }
        }

        foreach (Uri allowedAuthority in provider.AllowedAuthorities)
        {
            if (Uri.Compare(
                    endpoint,
                    allowedAuthority,
                    UriComponents.SchemeAndServer,
                    UriFormat.Unescaped,
                    StringComparison.OrdinalIgnoreCase) != 0)
            {
                continue;
            }

            if (IsAuthorityPathMatch(endpoint, allowedAuthority))
            {
                return endpoint;
            }
        }

        throw new InvalidOperationException(
            $"The Agent Card {endpointName} origin '{endpoint.GetLeftPart(UriPartial.Authority)}' "
            + "is not trusted by the selected OAuth provider.");
    }

    private static bool IsAuthorityPathMatch(Uri endpoint, Uri allowedAuthority)
    {
        string authorityPath = NormalizePath(allowedAuthority.AbsolutePath);
        if (authorityPath.Length == 0)
        {
            return true;
        }

        string endpointPath = NormalizePath(endpoint.AbsolutePath);
        return endpointPath.Equals(authorityPath, StringComparison.OrdinalIgnoreCase)
            || endpointPath.StartsWith(authorityPath + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
        => string.IsNullOrEmpty(path) || path == "/"
            ? string.Empty
            : path.TrimEnd('/');
}
