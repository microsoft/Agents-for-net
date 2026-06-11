// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Builder.UserAuth.EntraSidecar
{
    /// <summary>
    /// Result of a successful sidecar token acquisition.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SidecarTokenResult"/> class.
    /// </remarks>
    /// <param name="scheme">The authorization scheme (e.g. "Bearer").</param>
    /// <param name="token">The raw access token.</param>
    internal class SidecarTokenResult(string scheme, string token)
    {

        /// <summary>The authorization scheme (e.g., "Bearer" or "PoP").</summary>
        public string Scheme { get; } = scheme;

        /// <summary>The raw access token.</summary>
        public string Token { get; } = token;
    }
}
