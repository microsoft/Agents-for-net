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

        /// <summary>
        /// Gets or sets a value indicating whether each queued Activity is processed in its own dependency injection scope.
        /// </summary>
        /// <remarks>
        /// When <see langword="true"/> (the default), the <see cref="Microsoft.Agents.Builder.IAgent"/> and its dependencies
        /// are resolved from a scope that is disposed when the turn completes. Set this to <see langword="false"/> to resolve
        /// from the root <see cref="System.IServiceProvider"/> instead. Root resolution promotes scoped registrations in the
        /// Agent's dependency graph to the root scope, sharing one instance across turns for the lifetime of the process.
        /// </remarks>
        public bool UseScopedServices { get; set; } = true;
    }
}
