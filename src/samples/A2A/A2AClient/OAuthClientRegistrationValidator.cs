// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Samples.A2AClient;

/// <summary>
/// Validates a resolved client registration at the point credentials would leave the process.
/// </summary>
internal static class OAuthClientRegistrationValidator
{
    /// <summary>
    /// Rejects a registration whose client ID cannot identify a real OAuth client.
    /// </summary>
    /// <remarks>
    /// The shipped sample configuration carries an all-zero placeholder client ID. Every flow shares this check
    /// so Device Code, Authorization Code, Client Credentials, and refresh all fail the same way - before an
    /// authorization request is opened, a client secret is attached, or a token request is sent.
    /// </remarks>
    /// <param name="registration">The resolved client registration.</param>
    /// <exception cref="System.InvalidOperationException">
    /// The client ID is blank or is still the shipped placeholder.
    /// </exception>
    public static void EnsureUsableClientId(OAuthClientRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

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
}
