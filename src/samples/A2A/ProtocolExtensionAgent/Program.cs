// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.A2A;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace ProtocolExtensionAgent;

public static class Program
{
    public static async Task Main(string[] args)
    {
        await using var app = CreateApplication(args);
        await app.RunAsync();
    }

    internal static WebApplication CreateApplication(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddAgentDefaults().AddAgent<DebateAgent>();
        builder.Services.AddSingleton<IStorage, MemoryStorage>();
        builder.Services.AddA2AProtocolExtension<DebateExtension>();

        var app = builder.Build();
        app.UseAgents();
        app.MapA2AApplicationEndpoints(requireAuth: false);
        return app;
    }
}
