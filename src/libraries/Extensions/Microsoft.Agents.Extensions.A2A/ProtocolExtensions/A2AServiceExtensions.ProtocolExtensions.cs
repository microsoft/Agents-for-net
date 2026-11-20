// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Extensions.A2A.ProtocolExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Linq;

namespace Microsoft.Agents.Extensions.A2A;

public static partial class A2AServiceExtensions
{
    /// <summary>
    /// Adds an optional A2A protocol-extension contributor to a single-agent application.
    /// </summary>
    /// <typeparam name="TContributor">The singleton contributor type.</typeparam>
    /// <param name="services">The application service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// Most agents do not need custom protocol extensions. Multi-agent applications must use
    /// the agent-specific overload. Contributors compose with, and cannot replace, SDK-owned extensions.
    /// </remarks>
    public static IServiceCollection AddA2AProtocolExtension<TContributor>(this IServiceCollection services)
        where TContributor : class, IA2AProtocolExtensionContributor
        => AddProtocolExtension<TContributor>(services, null);

    /// <summary>
    /// Adds an optional A2A protocol-extension contributor only to the specified agent type.
    /// </summary>
    /// <typeparam name="TAgent">The mapped agent application type.</typeparam>
    /// <typeparam name="TContributor">The singleton contributor type.</typeparam>
    /// <param name="services">The application service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// Multiple contributors compose before the agent's registry is finalized. Repeated registration
    /// of the same contributor for the same agent is ignored. SDK-owned registrations remain authoritative.
    /// </remarks>
    public static IServiceCollection AddA2AProtocolExtension<TAgent, TContributor>(this IServiceCollection services)
        where TAgent : AgentApplication
        where TContributor : class, IA2AProtocolExtensionContributor
        => AddProtocolExtension<TContributor>(services, typeof(TAgent));

    private static IServiceCollection AddProtocolExtension<TContributor>(IServiceCollection services, Type agentType)
        where TContributor : class, IA2AProtocolExtensionContributor
    {
        ArgumentNullException.ThrowIfNull(services);
        AddProtocolExtensionRegistry(services);
        services.TryAddSingleton<TContributor>();
        if (!services.Any(service => service.ImplementationInstance is A2AProtocolExtensionRegistration registration
            && registration.AgentType == agentType
            && registration.ContributorType == typeof(TContributor)))
        {
            services.AddSingleton(new A2AProtocolExtensionRegistration(agentType, typeof(TContributor)));
        }
        return services;
    }

    private static void AddProtocolExtensionRegistry(IServiceCollection services)
    {
        services.TryAddSingleton(provider =>
        {
            var agent = services.LastOrDefault(service => service.ServiceType == typeof(IAgent) && !service.IsKeyedService);
            return new A2AProtocolExtensionRegistry(
                provider,
                provider.GetServices<A2AProtocolExtensionRegistration>(),
                agent?.ImplementationType ?? agent?.ImplementationInstance?.GetType());
        });
    }
}
