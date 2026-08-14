// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Registers services required by an Agents SDK extension.
    /// </summary>
    /// <remarks>
    /// Implementations referenced by <see cref="AgentServiceRegistrationAttribute"/> must be public and
    /// have a public parameterless constructor so consuming applications can preload and instantiate them.
    /// </remarks>
    public interface IAgentServiceRegistrar
    {
        /// <summary>
        /// Adds the extension's services to the application service collection.
        /// </summary>
        /// <param name="services">The service collection being configured.</param>
        void ConfigureServices(IServiceCollection services);
    }
}
