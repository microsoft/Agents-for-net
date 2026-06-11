// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Builder.UserAuth.EntraSidecar
{
    /// <summary>
    /// Settings for the Entra SDK Sidecar user authorization handler.
    /// </summary>
    public class SidecarSettings
    {
        /// <summary>
        /// The base URL of the Entra SDK Sidecar (e.g., "http://localhost:5178").
        /// If not set, resolves from the <c>SIDECAR_URL</c> environment variable,
        /// then falls back to <c>http://localhost:5178</c>.
        /// </summary>
        public string SidecarBaseUrl { get; set; }

        /// <summary>
        /// The resolved sidecar base URL after applying the environment variable fallback.
        /// </summary>
        public string ResolvedSidecarBaseUrl =>
            SidecarHttpClient.ResolveBaseUrl(SidecarBaseUrl);

        /// <summary>
        /// The configured downstream API service name in the sidecar's DownstreamApis configuration.
        /// </summary>
        public string ServiceName { get; set; }

        /// <summary>
        /// Optional scope overrides for the token request.
        /// </summary>
        public IList<string> Scopes { get; set; }

        /// <summary>
        /// When true, requests an app-only token instead of a user (delegated) token.
        /// Maps to <c>optionsOverride.RequestAppToken=true</c>.
        /// </summary>
        public bool RequestAppToken { get; set; }

        /// <summary>
        /// When true, forces a fresh token (bypasses sidecar cache).
        /// Maps to <c>optionsOverride.AcquireTokenOptions.ForceRefresh=true</c>.
        /// </summary>
        public bool ForceRefresh { get; set; }

        /// <summary>
        /// Optional tenant ID override for cross-tenant scenarios.
        /// Maps to <c>optionsOverride.AcquireTokenOptions.Tenant</c>.
        /// </summary>
        public string Tenant { get; set; }
    }
}
