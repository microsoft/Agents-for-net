// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Microsoft.Agents.Extensions.A2A.Hosting;

internal sealed record A2AGrpcEndpointMetadata(Type AgentType)
{
    internal static IAgent ResolveAgent(IServiceProvider services, Type agentType)
    {
        if (agentType != null && services.GetService(agentType) is IAgent agent)
        {
            return agent;
        }

        var defaultAgent = services.GetRequiredService<IAgent>();
        if (agentType != null && !agentType.IsInstanceOfType(defaultAgent))
        {
            throw new InvalidOperationException(
                $"The A2A agent '{agentType.FullName}' is not registered. Register it with AddAgent<TAgent>.");
        }
        return defaultAgent;
    }
}
