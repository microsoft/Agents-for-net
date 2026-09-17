// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Samples.A2AClient;

internal static class OAuthEndpointValidator
{
    public static Uri GetTrustedEndpoint(
        string? value,
        OAuthConnectionOptions connection,
        string endpointName)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? endpoint)
            || !endpoint.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The Agent Card {endpointName} must be an absolute HTTPS URI.");
        }

        foreach (Uri allowedOrigin in connection.AllowedOrigins)
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

        throw new InvalidOperationException(
            $"The Agent Card {endpointName} origin '{endpoint.GetLeftPart(UriPartial.Authority)}' "
            + "is not trusted by the selected OAuth connection.");
    }
}
