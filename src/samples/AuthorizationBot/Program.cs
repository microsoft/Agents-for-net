// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AuthorizationBot;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Samples;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();

builder.Logging.AddConsole();
builder.Logging.AddDebug();


// Add AspNet token validation
builder.Services.AddBotAspNetAuthentication(builder.Configuration);

// Add AgentApplicationOptions from config.
builder.AddAgentApplicationOptions(autoSignInSelector: (context, cancellationToken) => Task.FromResult(context.Activity.Text == "auto"));

// Add the bot (which is transient)
builder.AddBot<AuthBot>();


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

