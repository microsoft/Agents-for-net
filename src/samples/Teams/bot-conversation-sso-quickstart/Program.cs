// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using BotConversationSsoQuickstart;
using BotConversationSsoQuickstart.Bots;
using BotConversationSsoQuickstart.Dialogs;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.Agents.Samples;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
builder.AddBot<TeamsBot<MainDialog>, TeamsSSOAdapter>();

// Create the storage we'll be using for User and Conversation state. (Memory is great for testing purposes.)
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// Create the Conversation state.
builder.Services.AddSingleton<ConversationState>();

builder.Services.AddSingleton<IMiddleware[]>((sp) =>
{
    return 
    [
        new AutoSaveStateMiddleware(true, sp.GetService<ConversationState>()),
        new TeamsSSOTokenExchangeMiddleware(sp.GetService<IStorage>(), builder.Configuration["ConnectionName"])
    ];
});

// The Dialog that will be run by the bot.
builder.Services.AddSingleton<MainDialog>();

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
