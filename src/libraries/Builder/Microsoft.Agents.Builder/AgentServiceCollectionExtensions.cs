// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Dependency-injection registration methods for Agents SDK extensions.
    /// </summary>
    public static class AgentServiceCollectionExtensions
    {
        /// <summary>
        /// Applies service registrations declared by referenced Agents SDK extensions.
        /// </summary>
        /// <remarks>
        /// Extension assemblies opt in with <see cref="AgentServiceRegistrationAttribute"/>.
        /// Registrations are applied at most once per service collection.
        /// </remarks>
        /// <param name="services">The service collection to configure.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddAgentExtensionServices(this IServiceCollection services)
        {
            AgentServiceRegistrationAttribute.ConfigureServices(services);
            return services;
        }
    }
}
