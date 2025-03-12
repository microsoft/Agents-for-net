// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Samples;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.BotBuilder.State;
using TeamsConversationSsoQuickstart.Bots;
using TeamsConversationSsoQuickstart;
using TeamsConversationSsoQuickstart.Dialogs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();

builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add AspNet token validation
builder.Services.AddBotAspNetAuthentication(builder.Configuration);

// Add basic bot functionality
builder.AddBot<TeamsBot<MainDialog>, AdapterWithErrorHandler>();

// Add Conversation and User state.
builder.Services.AddSingleton<ConversationState>();
builder.Services.AddSingleton<UserState>();

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
