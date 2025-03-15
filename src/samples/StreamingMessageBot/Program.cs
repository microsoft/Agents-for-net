// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.App;
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
using StreamingMessageBot;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Logging.AddConsole();

// Add AspNet token validation
builder.Services.AddBotAspNetAuthentication(builder.Configuration);

// Add AgentApplicationOptions.  This will use DI'd services and IConfiguration for construction.
builder.Services.AddTransient<AgentApplicationOptions>();

builder.Services.AddTransient<IChatClient>(sp =>
{
    return new AzureOpenAIClient(new Uri(builder.Configuration["AIServices:AzureOpenAI:Endpoint"]), new ApiKeyCredential(builder.Configuration["AIServices:AzureOpenAI:ApiKey"]))
        .AsChatClient(builder.Configuration["AIServices:AzureOpenAI:DeploymentName"]);
});

// Add the bot (which is transient)
builder.AddBot<MyBot>();


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => "Microsoft Agents SDK Sample");
    app.UseDeveloperExceptionPage();
    app.MapControllers().AllowAnonymous();
}
else
{
    app.MapControllers();
}
app.Run();

