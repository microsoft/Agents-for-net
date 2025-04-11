// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Samples;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Azure.AI.OpenAI;
using System;
using System.ClientModel;
using Microsoft.Agents.Storage;
using StreamingMessageAgent;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Logging.AddConsole();

// Add AspNet token validation
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

builder.Services.AddTransient<IChatClient>(sp =>
{
    return new AzureOpenAIClient(new Uri(builder.Configuration["AIServices:AzureOpenAI:Endpoint"]), new ApiKeyCredential(builder.Configuration["AIServices:AzureOpenAI:ApiKey"]))
        .AsChatClient(builder.Configuration["AIServices:AzureOpenAI:DeploymentName"]);
});

// Add AgentApplicationOptions.  This will use DI'd services and IConfiguration for construction.
builder.AddAgentApplicationOptions();

// Add the Agent
builder.AddAgent<StreamingAgent>();

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operate correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => "Microsoft Agents SDK Sample - StreamingMessageAgent");
    app.UseDeveloperExceptionPage();
    app.MapControllers().AllowAnonymous();
}
else
{
    app.MapControllers();
}
app.Run();

