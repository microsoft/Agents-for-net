// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Extensions.Configuration;
using System;

namespace Microsoft.Agents.Authentication.EntraAuthSidecar.Model
{
    /// <summary>
    /// Connection settings for the sidecar-based token provider.
    /// </summary>
    public class SidecarConnectionSettings : ConnectionSettingsBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SidecarConnectionSettings"/> class.
        /// </summary>
        public SidecarConnectionSettings() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SidecarConnectionSettings"/> class from configuration.
        /// </summary>
        /// <param name="configurationSection">Configuration section with sidecar connection settings.</param>
        public SidecarConnectionSettings(IConfigurationSection configurationSection)
            : base(configurationSection)
        {
            ServiceName = configurationSection?.GetValue<string>("ServiceName", "default");
            BlueprintServiceName = configurationSection?.GetValue<string>("BlueprintServiceName", "agenticblueprint");
            SidecarBaseUrl = configurationSection?.GetValue<string>("SidecarBaseUrl");
            RequestTimeout = configurationSection?.GetValue("RequestTimeout", SidecarHttpClient.DefaultTimeout) ?? SidecarHttpClient.DefaultTimeout;
            RetryCount = configurationSection?.GetValue("RetryCount", SidecarHttpClient.DefaultRetryCount) ?? SidecarHttpClient.DefaultRetryCount;
            BypassLocalNetworkRestriction = configurationSection?.GetValue("BypassLocalNetworkRestriction", false) ?? false;
        }

        /// <summary>
        /// The configured downstream API service name in the sidecar's DownstreamApis configuration.
        /// </summary>
        public string ServiceName { get; set; } = "default";

        /// <summary>
        /// The sidecar downstream API name used to acquire the Blueprint (agent application) token for
        /// the agentic FIC chain. This downstream API must be configured app-only
        /// (<c>RequestAppToken: true</c>) with the <c>api://AzureAdTokenExchange/.default</c> scope.
        /// </summary>
        public string BlueprintServiceName { get; set; } = "agenticblueprint";

        /// <summary>
        /// Optional base URL of the Entra ID Agent Container (sidecar).
        /// Resolution order: <c>SIDECAR_URL</c> environment variable &gt; this setting &gt; default.
        /// Regardless of how it is resolved, the host must be a loopback/private address unless
        /// <see cref="BypassLocalNetworkRestriction"/> is set.
        /// </summary>
        public string SidecarBaseUrl { get; set; }

        /// <summary>
        /// When <c>true</c>, disables the loopback/private-address safety check on the resolved sidecar
        /// base URL. <b>UNSAFE.</b> Leave this <c>false</c> in all normal deployments. Only enable it for
        /// a carefully validated private-network configuration where the sidecar is reachable at a
        /// non-private address that the operator explicitly trusts; enabling it otherwise exposes the
        /// agent to SSRF and off-box credential exfiltration. Default: <c>false</c>.
        /// </summary>
        public bool BypassLocalNetworkRestriction { get; set; } = false;

        /// <summary>
        /// HTTP request timeout for sidecar calls. Default: 30 seconds.
        /// </summary>
        public TimeSpan RequestTimeout { get; set; } = SidecarHttpClient.DefaultTimeout;

        /// <summary>
        /// Number of retry attempts for transient sidecar failures (5xx, 408, 429, network/timeout).
        /// Default: 3.
        /// </summary>
        public int RetryCount { get; set; } = SidecarHttpClient.DefaultRetryCount;
    }
}
