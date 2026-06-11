// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AutoSignIn;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.UserAuth.TokenService;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

// Add the AgentApplication, which contains the logic for responding to
// user messages.
builder.AddAgent<AuthAgent>();

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operates correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// Add AspNet token validation for Azure Bot Service and Entra.  Authentication is
// configured in the appsettings.json "TokenValidation" section.
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

WebApplication app = builder.Build();

// Enable AspNet authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

// Map GET "/"
app.MapAgentRootEndpoint();

// Map the endpoints for all agents using the [AgentInterface] attribute.
// If there is a single IAgent/AgentApplication, the endpoints will be mapped to (e.g. "/api/message").
app.MapAgentApplicationEndpoints(requireAuth: !app.Environment.IsDevelopment());

// After MapAgentApplicationEndpoints, add:
app.MapGet("/auth/callback", async (HttpContext context, IStorage storage, IChannelAdapter adapter, IAgent agent, ILoggerFactory loggerFactory) =>
{
    var state = context.Request.Query["state"].ToString();
    var handler = new TokenServiceCallbackHandler(storage, adapter, agent, loggerFactory.CreateLogger<TokenServiceCallbackHandler>());
    var result = await handler.HandleAsync(state, context.RequestAborted);
    context.Response.StatusCode = result.StatusCode;
    await context.Response.WriteAsync(result.Message);
});

app.Run();
