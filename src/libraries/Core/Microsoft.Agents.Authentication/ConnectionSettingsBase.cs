// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Authentication
{
    /// <summary>
    /// Describes the connection settings for a given connection.
    /// </summary>
    public abstract class ConnectionSettingsBase : IConnectionSettings
    {
        protected ConnectionSettingsBase(IConfigurationSection configurationSection)
        {
            if (configurationSection != null && configurationSection.Exists())
            {
                ClientId = configurationSection.GetValue<string>("ClientId", null);
                Authority = configurationSection.GetValue<string>("AuthorityEndpoint", null);
                TenantId = configurationSection.GetValue<string>("TenantId", null);
                Scopes = configurationSection.GetSection("Scopes")?.Get<List<string>>();
            }
            else
            {
                if (configurationSection != null)
                {
                    throw new ArgumentException($"Authentication configuration section {configurationSection.Key}, not Found.");
                }
                else
                {
                    throw new ArgumentNullException(nameof(configurationSection), "No configuration section provided. An authentication configuration section is required to create a connection settings object.");
                }
            }
        }

        /// <summary>
        /// Client ID to use for the connection. 
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Login authority to use for the connection
        /// </summary>
        public string Authority { get; set; } = string.Empty;

        /// <summary>
        /// Tenant Id for creating the authentication for the connection 
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        public List<string> Scopes { get; set; } = [];
    }
}
