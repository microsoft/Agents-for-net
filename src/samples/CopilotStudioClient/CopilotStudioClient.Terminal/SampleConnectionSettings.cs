// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Extensions.Configuration;

namespace CopilotStudioClient.Terminal
{
    /// <summary>
    /// Connection Settings extension for the sample to include appID and TenantId for creating authentication token.
    /// </summary>
    internal class SampleConnectionSettings : ConnectionSettings
    {
        /// <summary>
        /// Use S2S connection for authentication.
        /// </summary>
        public bool UseS2SConnection { get; set; } = false;

        /// <summary>
        /// Tenant ID for creating the authentication for the connection
        /// </summary>
        public string? TenantId { get; set; }
        /// <summary>
        /// Application ID for creating the authentication for the connection
        /// </summary>
        public string? AppClientId { get; set; }

        /// <summary>
        /// Application secret for creating the authentication for the connection
        /// </summary>
        public string? AppClientSecret { get; set; }

        /// <summary>
        /// Create ConnectionSettings from a configuration section.
        /// </summary>
        /// <param name="config"></param>
        /// <exception cref="System.ArgumentException"></exception>
        public SampleConnectionSettings(IConfigurationSection config) : base(config)
        {
            ArgumentNullException.ThrowIfNull(config);

            DirectConnectUrl = config[nameof(DirectConnectUrl)];
            EnvironmentId = config[nameof(EnvironmentId)];
            SchemaName = config[nameof(SchemaName)];
            AppClientId = config[nameof(AppClientId)];
            TenantId = config[nameof(TenantId)];
            UseS2SConnection = config.GetValue<bool>(nameof(UseS2SConnection), false);
            AppClientSecret = config[nameof(AppClientSecret)];

            Validate();
        }

        private void Validate()
        {
            List<string> errors = [];

            if (string.IsNullOrWhiteSpace(AppClientId))
            {
                errors.Add($"{nameof(AppClientId)} must be nonblank.");
            }

            if (string.IsNullOrWhiteSpace(TenantId))
            {
                errors.Add($"{nameof(TenantId)} must be nonblank.");
            }

            if (string.IsNullOrWhiteSpace(DirectConnectUrl)
                && (string.IsNullOrWhiteSpace(EnvironmentId)
                    || string.IsNullOrWhiteSpace(SchemaName)))
            {
                errors.Add(
                    $"{nameof(DirectConnectUrl)} or both {nameof(EnvironmentId)} and {nameof(SchemaName)} must be nonblank.");
            }

            if (UseS2SConnection && string.IsNullOrWhiteSpace(AppClientSecret))
            {
                errors.Add($"{nameof(AppClientSecret)} must be nonblank when {nameof(UseS2SConnection)} is true.");
            }

            if (errors.Count > 0)
            {
                throw new ArgumentException(
                    $"Invalid CopilotStudioClientSettings: {string.Join(" ", errors)}");
            }
        }
    }
}
