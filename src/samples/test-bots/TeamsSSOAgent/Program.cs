// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Extensions.Teams.App.UserAuth;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading;
using TeamsSSOAgent;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();

builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add AgentApplicationOptions
builder.Services.AddSingleton(sp =>
{
    var storage = sp.GetService<IStorage>();

    var authOptions = new UserAuthorizationOptions(
        sp.GetService<IConnections>(),
        new TeamsSsoAuthentication(
            "auto",
            new TeamsSsoSettings("TeamsSsoConnection", ["User.Read"], $"{builder.Configuration["SignInPath"]}/auth-start.html"),
            sp.GetService<IConnections>(),
            storage))
    {
        // Auto-SignIn will use this OAuth flow
        DefaultHandlerName = "auto",
        AutoSignIn = UserAuthorizationOptions.AutoSignInOn
    };

    return new AgentApplicationOptions(storage)
    {
        Adapter = sp.GetService<IChannelAdapter>(),
        StartTypingTimer = false,
        UserAuthorization = authOptions
    };
});

// Add the Agent
builder.AddAgent<AuthAgent>();

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operate correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseRouting();
app.MapPost("/api/messages", async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
{
    await adapter.ProcessAsync(request, response, agent, cancellationToken);
})
    .AllowAnonymous();

// Hardcoded for brevity and ease of testing. 
// In production, this should be set in configuration.
app.Urls.Add($"http://localhost:3978");

app.MapGet("/", () => "Microsoft Agents SDK Sample");
app.UseStaticFiles();

app.Run();

