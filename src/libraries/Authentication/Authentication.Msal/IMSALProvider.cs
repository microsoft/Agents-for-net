// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Identity.Client;

namespace Microsoft.Agents.Authentication.Msal
{
    public interface IMSALProvider
    {
        IApplicationBase CreateClientApplication();

        /// <summary>
        /// Creates a client application configured with the specified redirect URI.
        /// Used for OAuth authorization code flows where the redirect URI must match
        /// the one used in the authorize request.
        /// </summary>
        /// <param name="redirectUri">The redirect URI to configure on the application.</param>
        /// <returns>An MSAL application configured with the redirect URI.</returns>
        IApplicationBase CreateClientApplication(string redirectUri);

        /// <summary>
        /// Gets or creates a cached client application configured with the specified redirect URI.
        /// The cached instance retains its MSAL token cache across calls, enabling AcquireTokenSilent.
        /// </summary>
        /// <param name="redirectUri">The redirect URI to configure on the application.</param>
        /// <returns>A cached MSAL application configured with the redirect URI.</returns>
        IConfidentialClientApplication GetOrCreateConfidentialClient(string redirectUri);
    }
}
