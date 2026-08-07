// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using System;

namespace Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue
{
    /// <summary>
    /// Configuration options for the hosted activity service.
    /// </summary>
    public class HostedActivityServiceOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HostedActivityServiceOptions"/> class.
        /// </summary>
        /// <param name="configuration">The application configuration.</param>
        public HostedActivityServiceOptions(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            configuration.GetSection(nameof(HostedActivityServiceOptions)).Bind(this);
        }

        /// <summary>
        /// Gets or sets the maximum number of seconds to wait for activity processing during shutdown.
        /// </summary>
        public int ShutdownTimeoutSeconds { get; set; } = 60;
    }
}
