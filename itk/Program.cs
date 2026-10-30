// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AgentsA2AItk;
using Microsoft.Agents.Extensions.A2A;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;

int httpPort = 3978;
for (int index = 0; index < args.Length; index++)
{
    if (args[index] == "--httpPort" && index + 1 < args.Length)
    {
        httpPort = int.Parse(args[++index], System.Globalization.CultureInfo.InvariantCulture);
    }
    else if (args[index] == "--grpcPort" && index + 1 < args.Length)
    {
        index++; // The extension does not expose a gRPC binding.
    }
}

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://127.0.0.1:{httpPort}");
builder.AddAgentDefaults().AddAgent<ItkAgent>();
builder.Services.AddSingleton<IStorage, MemoryStorage>();
builder.Services.AddTransient<LoopbackHttpHandler>();
builder.Services.AddHttpClient("itk-peers")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
    .AddHttpMessageHandler<LoopbackHttpHandler>();

var app = builder.Build();
app.Use((context, next) =>
{
    // ACTS sends this synthetic bearer token on every abstract operation, even
    // when the Agent Card advertises no authentication. This local harness is
    // anonymous, so do not pass that non-JWT fixture to the SDK auth parser.
    if (string.Equals(context.Request.Headers.Authorization.ToString(), "Bearer itk-valid-token", StringComparison.Ordinal))
    {
        context.Request.Headers.Remove("Authorization");
    }

    return next();
});
app.UseAgents();
app.MapA2AApplicationEndpoints(requireAuth: false, defaultPath: "/jsonrpc");
app.Run();
