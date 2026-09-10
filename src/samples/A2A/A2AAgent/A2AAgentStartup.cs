// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.A2A;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace A2AAgent;

/// <summary>
/// Startup composition for the sample, factored out of <c>Program.cs</c> so the authentication
/// decision can be verified by tests without starting a host.
/// </summary>
internal static class A2AAgentStartup
{
    internal const string TokenValidationSectionName = "TokenValidation";

    internal static void ConfigureBuilder(WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddAgentDefaults()
            .AddAgent<MyAgent>()
            .AddAgentAuthorization(
                b => b.AddAgentAspNetAuthentication(TokenValidationSectionName),
                forceEnable: ShouldEnableTokenValidation(builder.Configuration, builder.Environment));

        builder.Services.AddHttpClient<IGraphProfileClient, GraphProfileClient>(client =>
        {
            client.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/");
        });

        // Register IStorage.  For development, MemoryStorage is suitable.
        // For production Agents, persisted storage should be used so
        // that state survives Agent restarts, and operate correctly
        // in a cluster of Agent instances.
        builder.Services.AddSingleton<IStorage, MemoryStorage>();
    }

    internal static void ConfigureApplication(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Add the authentication and authorization middleware to the request pipeline.
        app.UseAgents();

        // Map the default agent endpoints: GET "/" and the agent message endpoints.
        app.MapDefaultAgentEndpoints();

        // Map A2A endpoints.  By default A2A will respond on '/a2a'.
        // The A2A transport itself stays anonymous even when token validation is enabled: the
        // '-delegated', '-me', and '-app' routes opt in individually through autoSignInHandlers,
        // and the echo, multi-turn, streaming, and direct A2A routes remain anonymous.
        app.MapA2AApplicationEndpoints(requireAuth: false);
    }

    /// <summary>
    /// Decides whether ASP.NET Core token validation is registered.
    /// </summary>
    /// <remarks>
    /// <see cref="AgentHostExtensions.AddAgentAuthorization"/> defaults to disabling authentication in
    /// the Development environment, and <c>Properties\launchSettings.json</c> pins Development. Without
    /// this override, <c>dotnet run</c> would register no authentication scheme, no request could carry a
    /// validated bearer token, and the route-scoped OAuth routes could never succeed locally. Token
    /// validation is therefore also enabled in Development once real <c>TokenValidation</c> settings are
    /// present, while the shipped placeholder configuration keeps the anonymous-only run working.
    /// </remarks>
    internal static bool ShouldEnableTokenValidation(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        return !environment.IsDevelopment() || IsTokenValidationConfigured(configuration);
    }

    /// <summary>
    /// Indicates whether <c>TokenValidation:Audiences</c> holds real values rather than the shipped
    /// <c>{{ClientId}}</c> placeholder. <c>AddAgentAspNetAuthentication</c> requires every audience to be
    /// a GUID, so anything else cannot produce a working authentication scheme.
    /// </summary>
    internal static bool IsTokenValidationConfigured(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        IConfigurationSection section = configuration.GetSection(TokenValidationSectionName);
        if (!section.Exists())
        {
            return false;
        }

        List<string?> audiences = section.GetSection("Audiences").GetChildren().Select(child => child.Value).ToList();
        return audiences.Count > 0 && audiences.TrueForAll(audience => Guid.TryParse(audience, out _));
    }
}
