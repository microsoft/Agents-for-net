// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    /// <summary>
    /// Configuration options for CloudAdapter runtime behavior.
    /// </summary>
    public class AdapterOptions
    {
        /// <summary>
        /// Gets or sets the maximum number of seconds to wait for the application to shut down gracefully. 
        /// </summary>
        /// <remarks>If the shutdown process does not complete within the specified timeout, the
        /// application may be terminated forcefully. Set this value according to the expected shutdown duration of your
        /// application components.</remarks>
        [Obsolete("Use HostedActivityServiceOptions or HostedTaskServiceOptions instead.")]
        public int ShutdownTimeoutSeconds { get; set; } = 60;

        /// <summary>
        /// Gets or sets a value indicating whether stack traces should be emitted in OnTurnError output.
        /// </summary>
        public bool EmitStackTrace { get; set; } = false;

    }
}
