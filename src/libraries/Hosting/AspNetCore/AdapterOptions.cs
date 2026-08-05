// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

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
        public int ShutdownTimeoutSeconds { get; set; } = 60;

        /// <summary>
        /// Gets or sets a value indicating whether stack traces should be emitted in OnTurnError output.
        /// </summary>
        public bool EmitStackTrace { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether Activity.ServiceUrl should be validated using the 'serviceurl' claim in the incoming token. This is typically used to ensure that the request is coming from a trusted source.
        /// </summary>
        public bool ValidateServiceUrl { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether each queued Activity is processed in its own dependency injection scope.
        /// </summary>
        /// <remarks>
        /// When <see langword="false"/> (the default), queued Activities resolve the <see cref="Microsoft.Agents.Builder.IAgent"/>
        /// from the root <see cref="System.IServiceProvider"/>. Any scoped registration in the Agent's dependency graph is then
        /// promoted to the root scope, giving a single instance shared by every turn for the lifetime of the process.
        /// Set this to <see langword="true"/> for Agents that depend on scoped services, such as an Entity Framework Core
        /// <c>DbContext</c>. The SDK registers no scoped services, so enabling this affects only registrations made by the
        /// application. Note that disposable transient dependencies resolved for a turn - <see cref="Microsoft.Agents.Builder.IAgent"/>
        /// itself is registered transient - are then disposed with the turn scope, rather than being retained by the root
        /// scope until the host shuts down.
        /// </remarks>
        public bool UseScopePerTurn { get; set; } = false;
    }
}
