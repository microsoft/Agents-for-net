// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Agents.Hosting.AgentRunner;

internal static class AgentRunnerHost
{
    internal static WebApplication BuildApp(AgentRunnerArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var directory = Path.GetDirectoryName(Path.GetFullPath(args.AgentAssemblyPath))!;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production,
            ContentRootPath = directory,
        });
        AddAgentSettings(builder, directory);
        AgentAssemblyLoader.LoadAndRegister(builder, args.AgentAssemblyPath);
        AgentRunnerAuthentication.AddTokenValidation(builder.Services, builder.Configuration);
        builder.Services.AddAuthorization();

        var app = builder.Build();
        _ = app.Services.GetRequiredService<IAgentHttpAdapter>();
        _ = app.Services.GetRequiredService<IAgent>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapPost("/api/messages", async (
            HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter,
            IAgent agent, CancellationToken cancellationToken) =>
        {
            await adapter.ProcessAsync(request, response, agent, cancellationToken).ConfigureAwait(false);
        }).RequireAuthorization();

        app.Urls.Clear();
        app.Urls.Add($"http://127.0.0.1:{args.Port}");
        return app;
    }

    internal static void AddAgentSettings(WebApplicationBuilder builder, string directory)
    {
        builder.Configuration.AddJsonFile(Path.Combine(directory, "appsettings.json"),
            optional: true, reloadOnChange: false);
        builder.Configuration.AddEnvironmentVariables();
    }
}
