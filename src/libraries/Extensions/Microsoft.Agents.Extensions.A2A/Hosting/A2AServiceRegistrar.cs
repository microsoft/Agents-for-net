// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Agents.Extensions.A2A.Hosting;

public sealed class A2AServiceRegistrar : IAgentServiceRegistrar
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddA2AAdapter();
    }
}
