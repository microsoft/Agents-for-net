// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Agents.Builder.UserAuth.EntraSidecar
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
    }
}
