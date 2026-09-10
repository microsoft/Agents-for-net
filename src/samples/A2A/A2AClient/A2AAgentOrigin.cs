// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Samples.A2AClient;

/// <summary>
/// Origin rules that decide where the client is willing to send an Agent API access token.
/// </summary>
/// <remarks>
/// The Agent Card is fetched from the agent itself, but its advertised interface URLs are data. A
/// compromised or misconfigured agent could advertise an interface hosted elsewhere, and attaching the
/// Agent API bearer token to that URL would hand the caller's credential to a third party. Token
/// attachment and interface selection are therefore both constrained to the configured agent origin.
/// </remarks>
internal static class A2AAgentOrigin
{
    /// <summary>
    /// Compares scheme, host, and effective port. <see cref="Uri.Port"/> already resolves the default
    /// port for the scheme, so <c>https://host</c> and <c>https://host:443</c> compare equal.
    /// </summary>
    public static bool IsSameOrigin(Uri agentOrigin, Uri? candidate)
    {
        ArgumentNullException.ThrowIfNull(agentOrigin);

        if (candidate is null || !candidate.IsAbsoluteUri)
        {
            return false;
        }

        return string.Equals(agentOrigin.Scheme, candidate.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(agentOrigin.Host, candidate.Host, StringComparison.OrdinalIgnoreCase)
            && agentOrigin.Port == candidate.Port;
    }

    /// <summary>
    /// Indicates whether a credential may be sent to the URL at all. Plaintext HTTP is only allowed for
    /// loopback addresses, which is how the sample runs locally.
    /// </summary>
    public static bool IsSecureTarget(Uri? candidate)
    {
        if (candidate is null || !candidate.IsAbsoluteUri)
        {
            return false;
        }

        return candidate.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || candidate.IsLoopback;
    }

    /// <summary>
    /// Throws before any credential is attached when the target is not the configured agent origin or is
    /// not a secure destination.
    /// </summary>
    public static void EnsureCredentialTarget(Uri agentOrigin, Uri? candidate)
    {
        ArgumentNullException.ThrowIfNull(agentOrigin);

        if (!IsSameOrigin(agentOrigin, candidate))
        {
            throw new InvalidOperationException(
                $"Refusing to send the Agent API access token to '{Describe(candidate)}' because it is not the configured agent origin '{Describe(agentOrigin)}'.");
        }

        if (!IsSecureTarget(candidate))
        {
            throw new InvalidOperationException(
                $"Refusing to send the Agent API access token to '{Describe(candidate)}' over plaintext HTTP. Use HTTPS or a loopback address.");
        }
    }

    /// <summary>
    /// Reduces a configured agent URL to its origin.
    /// </summary>
    public static Uri FromAgentUrl(Uri agentUrl)
    {
        ArgumentNullException.ThrowIfNull(agentUrl);

        if (!agentUrl.IsAbsoluteUri)
        {
            throw new InvalidOperationException("The configured agent URL must be an absolute URI.");
        }

        return new UriBuilder(agentUrl.Scheme, agentUrl.Host, agentUrl.Port).Uri;
    }

    private static string Describe(Uri? uri)
        => uri is null ? "(no address)"
            : uri.IsAbsoluteUri ? uri.GetLeftPart(UriPartial.Authority)
            : uri.OriginalString;
}
