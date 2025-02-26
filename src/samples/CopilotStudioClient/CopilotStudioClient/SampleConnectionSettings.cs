using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Agents.CopilotStudio.Client.Discovery;

namespace CopilotStudioClientSample
{
    /// <summary>
    /// Connection Settings extension for the sample to include appID and TeantId for creating authentication token.
    /// </summary>
    internal class SampleConnectionSettings : ConnectionSettings
    {
        /// <summary>
        /// Tenant ID for creating the authentication for the connection
        /// </summary>
        public string? TenantId { get; set; }
        /// <summary>
        /// Application ID for creating the authentication for the connection
        /// </summary>
        public string? AppClientId { get; set; }

        /// <summary>
        /// Create ConnectionSettings from a configuration section.
        /// </summary>
        /// <param name="config"></param>
        /// <exception cref="ArgumentException"></exception>
        public SampleConnectionSettings(IConfigurationSection config) :base (config)
        {
            AppClientId = config[nameof(AppClientId)] ?? throw new ArgumentException($"{nameof(AppClientId)} not found in config");
            TenantId = config[nameof(TenantId)] ?? throw new ArgumentException($"{nameof(TenantId)} not found in config");
        }
    }
}
