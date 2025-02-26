// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CopilotStudioEchoSkill;
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
builder.Logging.AddDebug();

// Add AspNet token validation
builder.Services.AddBotAspNetAuthentication(builder.Configuration);

// Add bot routes and logic
builder.AddBot<BotAdapterWithErrorHandler>(sp =>
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
                await turnContext.SendActivityAsync(MessageFactory.Text("Hi, This is EchoSkill"), cancellationToken);
            }
        }
    });

    app.OnActivity(ActivityTypes.EndOfConversation, async (turnContext, turnState, cancellationToken) =>
    {
        // This will be called if the root bot is ending the conversation.  Sending additional messages should be
        // avoided as the conversation may have been deleted.
        // Perform cleanup of resources if needed.
       await turnContext.SendActivityAsync("Received EndOfConversation", cancellationToken: cancellationToken);
    });

    // Listen for ANY message to be received. MUST BE AFTER ANY OTHER MESSAGE HANDLERS
    app.OnActivity(ActivityTypes.Message, async (turnContext, turnState, cancellationToken) =>
    {
        if (turnContext.Activity.Text.Contains("end") || turnContext.Activity.Text.Contains("stop"))
        {
            var messageText = $"(EchoSkill) Ending conversation...";
            await turnContext.SendActivityAsync(MessageFactory.Text(messageText, messageText, InputHints.IgnoringInput.ToString()), cancellationToken);

            // Indicate this conversation is over by sending an EndOfConversation Activity.
            // This bot doesn't return a value, but if it did it could be put in Activity.Value.
            var endOfConversation = Activity.CreateEndOfConversationActivity();
            endOfConversation.Code = EndOfConversationCodes.CompletedSuccessfully;
            await turnContext.SendActivityAsync(endOfConversation, cancellationToken);
        }
        else
        {
            var messageText = $"Echo(EchoSkill): {turnContext.Activity.Text}";
            await turnContext.SendActivityAsync(MessageFactory.Text(messageText, messageText, InputHints.IgnoringInput.ToString()), cancellationToken);
            messageText = "Echo(EchoSkill): Say \"end\" or \"stop\" and I'll end the conversation and return to the parent.";
            await turnContext.SendActivityAsync(MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput.ToString()), cancellationToken);
        }
    });

    return app;
});

var app = builder.Build();

// Required for providing the bot manifest.
app.UseHttpsRedirection();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => "Microsoft Agents SDK Sample - EchoSkill");
    app.UseDeveloperExceptionPage();
    app.MapControllers().AllowAnonymous();
}
else
{
    app.MapControllers();
}

app.Run();
