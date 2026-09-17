// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Samples.A2AClient;

internal static class GitHubDeviceFlowAuthentication
{
    private const string GitHubHost = "github.com";
    private const string DeviceAuthorizationPath = "/login/device/code";
    private const string TokenPath = "/login/oauth/access_token";

    public static bool IsSupported(A2AAgentCardAuthentication authentication)
    {
        ArgumentNullException.ThrowIfNull(authentication);

        return authentication.Mode == A2AAuthMode.Delegated
            && TryCreateEndpoint(authentication.TokenUrl, TokenPath, out _)
            && TryCreateEndpoint(authentication.DeviceAuthorizationUrl, DeviceAuthorizationPath, out _);
    }

    public static Uri GetRequiredDeviceAuthorizationEndpoint(A2AAgentCardAuthentication authentication)
        => GetRequiredEndpoint(authentication, authentication.DeviceAuthorizationUrl, DeviceAuthorizationPath, "device authorization");

    public static Uri GetRequiredTokenEndpoint(A2AAgentCardAuthentication authentication)
        => GetRequiredEndpoint(authentication, authentication.TokenUrl, TokenPath, "token");

    public static void Validate(A2AAgentCardAuthentication authentication)
    {
        ArgumentNullException.ThrowIfNull(authentication);

        if (authentication.Mode != A2AAuthMode.Delegated)
        {
            throw new InvalidOperationException(
                $"Security scheme '{authentication.SecuritySchemeName}' must use delegated Device Code authentication to acquire a GitHub user token.");
        }

        _ = GetRequiredTokenEndpoint(authentication);
        _ = GetRequiredDeviceAuthorizationEndpoint(authentication);
    }

    private static Uri GetRequiredEndpoint(
        A2AAgentCardAuthentication authentication,
        string? value,
        string expectedPath,
        string endpointName)
    {
        if (TryCreateEndpoint(value, expectedPath, out Uri? endpoint))
        {
            return endpoint!;
        }

        throw new InvalidOperationException(
            $"Security scheme '{authentication.SecuritySchemeName}' must provide the GitHub {endpointName} endpoint 'https://{GitHubHost}{expectedPath}' with no query or fragment.");
    }

    private static bool TryCreateEndpoint(string? value, string expectedPath, out Uri? endpoint)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? candidate)
            && candidate.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && candidate.Host.Equals(GitHubHost, StringComparison.OrdinalIgnoreCase)
            && candidate.IsDefaultPort
            && candidate.AbsolutePath.Equals(expectedPath, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrEmpty(candidate.Query)
            && string.IsNullOrEmpty(candidate.Fragment))
        {
            endpoint = candidate;
            return true;
        }

        endpoint = null;
        return false;
    }
}
