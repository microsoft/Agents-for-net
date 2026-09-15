// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Agents.Extensions.A2A.Integration;

/// <summary>
/// Registers the A2A adapter when an agent application loads extension service registrars.
/// </summary>
public sealed class A2AServiceRegistrar : IAgentServiceRegistrar
{
    /// <summary>
    /// Adds A2A adapter services to the application service collection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddA2AAdapter();
    }
}
