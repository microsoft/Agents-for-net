// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using EchoBot;
using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Samples;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Logging.AddConsole();

// Add AspNet token validation
builder.Services.AddBotAspNetAuthentication(builder.Configuration);

// Add bot routes and logic
builder.AddBot(sp =>
{
    var options = new ApplicationOptions()
    {
        StartTypingTimer = false,
        TurnStateFactory = () => new TurnState(sp.GetService<IStorage>())
    };

    var app = new Application(options);

    // Display a welcome message
    app.OnConversationUpdate(ConversationUpdateEvents.MembersAdded, async (turnContext, turnState, cancellationToken) =>
    {
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Hello and Welcome!"), cancellationToken);
            }
        }
    });

    // Listen for user to say "/reset" and then delete conversation state
    app.OnMessage("/reset", async (turnContext, turnState, cancellationToken) =>
    {
        await turnState.Conversation.DeleteStateAsync(turnContext, cancellationToken);
        await turnContext.SendActivityAsync("Ok I've deleted the current conversation state", cancellationToken: cancellationToken);
    });

    // Listen for ANY message to be received. MUST BE AFTER ANY OTHER MESSAGE HANDLERS
    app.OnActivity(ActivityTypes.Message, async (turnContext, turnState, cancellationToken) =>
    {
        // Increment count state.
        int count = turnState.Conversation.IncrementMessageCount();

        await turnContext.SendActivityAsync($"[{count}] you said: {turnContext.Activity.Text}", cancellationToken: cancellationToken);
    });

    return app;
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

