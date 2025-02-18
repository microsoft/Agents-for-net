// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Samples;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.Hosting.AspNetCore;
using AuthenticationBot;
using Microsoft.Agents.Extensions.Teams.Compat;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.Compat;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();

builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add AspNet token validation
builder.Services.AddBotAspNetAuthentication(builder.Configuration);

// Add basic bot functionality
builder.AddBot<AuthBot>();

// Add IStorage for turn state persistence
builder.Services.AddSingleton<IStorage, MemoryStorage>();

builder.Services.AddTransient<PrivateConversationState>();

builder.Services.AddSingleton<IMiddleware[]>((sp) =>
{
    return 
    [
        new AutoSaveStateMiddleware(true, new PrivateConversationState(sp.GetService<IStorage>())),
        new TeamsSSOTokenExchangeMiddleware(sp.GetService<IStorage>(), builder.Configuration["ConnectionName"])
    ];
});

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

