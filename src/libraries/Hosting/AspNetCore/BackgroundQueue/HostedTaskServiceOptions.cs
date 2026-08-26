// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using System;

namespace Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue
{
    /// <summary>
    /// Configuration options for the hosted task service.
    /// </summary>
    public class HostedTaskServiceOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HostedTaskServiceOptions"/> class.
        /// </summary>
        /// <param name="configuration">The application configuration.</param>
        public HostedTaskServiceOptions(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            configuration.GetSection(nameof(HostedTaskServiceOptions)).Bind(this);
        }

        /// <summary>
        /// Gets or sets the maximum number of seconds to wait for task processing during shutdown.
        /// </summary>
        public int ShutdownTimeoutSeconds { get; set; } = 60;
    }
}
