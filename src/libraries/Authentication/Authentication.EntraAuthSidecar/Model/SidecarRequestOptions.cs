// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Authentication.EntraAuthSidecar.Model
{
    /// <summary>
    /// Options used to build the query string for a sidecar token request.
    /// </summary>
    internal class SidecarRequestOptions
    {
        /// <summary>Agent app (client) ID for agent identity flows. Maps to <c>AgentIdentity</c>.</summary>
        public string AgentIdentity { get; set; }

        /// <summary>Agentic user principal name for delegated agent flows. Maps to <c>AgentUsername</c>.</summary>
        public string AgentUsername { get; set; }

        /// <summary>Agentic user object ID for delegated agent flows. Maps to <c>AgentUserId</c>.</summary>
        public string AgentUserId { get; set; }

        /// <summary>Override the configured downstream API scopes. Maps to <c>optionsOverride.Scopes</c>.</summary>
        public IList<string> Scopes { get; set; }

        /// <summary>
        /// Request an app-only token instead of a user (delegated) token.
        /// Maps to <c>optionsOverride.RequestAppToken=true</c>.
        /// </summary>
        public bool? RequestAppToken { get; set; }

        /// <summary>Override the tenant ID. Maps to <c>optionsOverride.AcquireTokenOptions.Tenant</c>.</summary>
        public string Tenant { get; set; }

        /// <summary>
        /// Force a fresh token acquisition, bypassing the sidecar cache.
        /// Maps to <c>optionsOverride.AcquireTokenOptions.ForceRefresh=true</c>.
        /// </summary>
        public bool? ForceRefresh { get; set; }
    }
}
