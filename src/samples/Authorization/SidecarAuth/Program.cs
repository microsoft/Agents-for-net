// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using SidecarAuth;
using Microsoft.Agents.Authentication.EntraAuthSidecar;
using Microsoft.Agents.Authentication.EntraAuthSidecar.HealthChecks;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

// Add the AgentApplication, which contains the logic for responding to
// user messages.
builder.AddAgent<SidecarAgent>();

// Report whether the Entra ID sidecar is reachable at "/health".
builder.Services.AddHealthChecks().AddSidecarHealthCheck();

// Optionally probe the sidecar once at startup. With failOnUnreachable:false (the default) an
// unreachable sidecar logs a warning and startup continues; set true to fail fast instead.
builder.Services.AddSidecarStartupProbe(failOnUnreachable: false);

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

// Sidecar reachability health endpoint.
app.MapHealthChecks("/health");

// Map the endpoints for all agents using the [AgentInterface] attribute.
// If there is a single IAgent/AgentApplication, the endpoints will be mapped to (e.g. "/api/message").
app.MapAgentApplicationEndpoints(requireAuth: true);

app.Run();
