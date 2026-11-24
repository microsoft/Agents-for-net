// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.A2A;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ProtocolExtensionAgent;

internal static class ProtocolExtensionAgentStartup
{
    internal static void ConfigureBuilder(WebApplicationBuilder builder)
    {
        builder.AddAgentDefaults().AddAgent<DebateAgent>();
        builder.Services.AddSingleton<IStorage, MemoryStorage>();
        builder.Services.AddA2AProtocolExtension<DebateExtension>();
    }

    internal static void ConfigureApplication(WebApplication app)
    {
        app.UseAgents();
        app.MapA2AApplicationEndpoints(requireAuth: false);
    }
}
