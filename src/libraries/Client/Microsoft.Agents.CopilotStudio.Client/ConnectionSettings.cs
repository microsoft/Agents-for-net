// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Agents.CopilotStudio.Client.Discovery;
using Microsoft.Agents.CopilotStudio.Client.Interfaces;

namespace Microsoft.Agents.CopilotStudio.Client
{
    /// <summary>
    /// Configuration object for the DirectToEngine client.
    /// </summary>
    public class ConnectionSettings : ICopilotStudioClientConnectionSettings
    {
        //<inheritdoc/>
        public string? EnvironmentId { get; set; }
        //<inheritdoc/>
        public PowerPlatformCloud? Cloud { get; set; }
        //<inheritdoc/>
        public string? CustomPowerPlatformCloud { get; set; }
        //<inheritdoc/>
        public string? SchemaName { get; set; }
        //<inheritdoc/>
        public BotType? CopilotBotType { get; set; }
        //<inheritdoc/>
        public string? DirectConnectUrl { get; set; } = null;
        //<inheritdoc/>
        public bool UseExperimentalEndpoint { get; set; } = false;
        //<inheritdoc/>
        public bool EnableDiagnostics { get; set; } = false;

        }

        /// <summary>
        /// Create ConnectionSettings from a configuration section.
        /// </summary>
        /// <param name="config">Configuration Section containing DirectToEngine Connection settings</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public ConnectionSettings(IConfigurationSection config)
        {
            if (config != null && config.Exists())
            {
                DirectConnectUrl = config[nameof(DirectConnectUrl)];
                Cloud = config.GetValue(nameof(Cloud), PowerPlatformCloud.Prod);
                CopilotBotType = config.GetValue(nameof(CopilotBotType), BotType.Published);
                CustomPowerPlatformCloud = config[nameof(CustomPowerPlatformCloud)];
                UseExperimentalEndpoint = config.GetValue<bool>(nameof(UseExperimentalEndpoint),false);
                EnableDiagnostics = config.GetValue<bool>(nameof(EnableDiagnostics), false);
                if (string.IsNullOrEmpty(DirectConnectUrl))
                {
                    EnvironmentId = config[nameof(EnvironmentId)] ?? throw new ArgumentException($"{nameof(EnvironmentId)} not found in config");
                    BotIdentifier = config[nameof(BotIdentifier)] ?? throw new ArgumentException($"{nameof(BotIdentifier)} not found in config");
                }
            }
        }
    }
}
