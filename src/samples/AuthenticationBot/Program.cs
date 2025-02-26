// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.BotBuilder.App.UserAuth;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.BotBuilder.UserAuth.TokenService;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Samples;
using Microsoft.Agents.Storage;
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

// Create the bot as a transient.
builder.Services.AddTransient<ITurnState>(sp => new TurnState(sp.GetService<IStorage>()));
builder.AddBot((sp) =>
{
    var adapter = sp.GetService<IChannelAdapter>();
    var storage = sp.GetService<IStorage>();

    var authOptions = new UserAuthenticationOptions()
    {
        // Auto-SignIn will use this OAuth flow
        Default = "graph",

        AutoSignIn = (context, cancellationToken) =>
        {
            return Task.FromResult(context.Activity.Text == "auto");
        },

        Handlers =
        [
            new OAuthAuthentication(
                "graph",
                new OAuthSettings()
                {
                    ConnectionName = builder.Configuration["ConnectionName"]
                },
                storage)]
    };

    var appOptions = new ApplicationOptions()
    {
        Adapter = adapter,
        StartTypingTimer = true,
        UserAuthentication = authOptions,
        TurnStateFactory = () => sp.GetService<ITurnState>()
    };

    var app = new Application(appOptions);

    app.Authentication.OnUserSignInSuccess(async (turnContext, turnState, flowName, tokenResponse, cancellationToken) =>
    {
        await turnContext.SendActivityAsync($"Successfully logged in to '{flowName}'", cancellationToken: cancellationToken);
    });

    app.Authentication.OnUserSignInFailure(async (turnContext, turnState, flowName, response, cancellationToken) =>
    {
        await turnContext.SendActivityAsync($"Failed to login to '{flowName}': {response.Error.Message}", cancellationToken: cancellationToken);
    });

    app.OnMessage("/signin", async (turnContext, turnState, cancellationToken) =>
    {
        await app.Authentication.GetTokenOrStartSignInAsync(turnContext, turnState, "graph", cancellationToken);
    });

    // Listen for user to say "/reset" and then delete state
    app.OnMessage("/reset", async (turnContext, turnState, cancellationToken) =>
    {
        await turnState.Conversation.DeleteStateAsync(turnContext, cancellationToken);
        await turnState.User.DeleteStateAsync(turnContext, cancellationToken);
        await turnContext.SendActivityAsync("Ok I've deleted the current turn state", cancellationToken: cancellationToken);
    });

    // Listen for user to say "/signout" and then delete cached token
    app.OnMessage("/signout", async (turnContext, turnState, cancellationToken) =>
    {
        await app.Authentication.SignOutUserAsync(turnContext, turnState, cancellationToken: cancellationToken);
        await turnContext.SendActivityAsync("You have signed out", cancellationToken: cancellationToken);
    });

    // Display a welcome message
    app.OnConversationUpdate(ConversationUpdateEvents.MembersAdded, async (turnContext, turnState, cancellationToken) =>
    {
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Welcome to AuthenticationBot. Type 'auto' to demonstrate Auto SignIn. Type '/signin' to sign in for graph.  Type '/signout' to sign-out.  Anything else will be repeated back."), cancellationToken);
            }
        }
    });

    // Listen for ANY message to be received. MUST BE AFTER ANY OTHER MESSAGE HANDLERS
    app.OnActivity(ActivityTypes.Message, async (turnContext, turnState, cancellationToken) =>
    {
        if (turnContext.Activity.Text == "auto")
        {
            await turnContext.SendActivityAsync($"Successfully logged in to '{app.Authentication.Default}', token length: {turnState.Temp.AuthTokens[app.Authentication.Default].Length}", cancellationToken: cancellationToken);
        }
        else
        {
            await turnContext.SendActivityAsync($"You said: {turnContext.Activity.Text}", cancellationToken: cancellationToken);
        }
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

