// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.BotBuilder.UserAuth;
using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Agents.CopilotStudio.Client.Discovery;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OBOAuthorizationBot;

public class OBOAuth : AgentApplication
{
    private readonly IHttpClientFactory _httpClientFactory;

    private ConnectionSettings _mcsSettings = new ConnectionSettings()
    {
        EnvironmentId = "Default-69e9b82d-4842-4902-8d1e-abc5b98a55e8",
        BotIdentifier = "cref7_agent",
        CopilotBotType = BotType.Published,
        Cloud = PowerPlatformCloud.Prod,
    };

    public OBOAuth(AgentApplicationOptions options, IHttpClientFactory clientFactory) : base(options)
    {
        _httpClientFactory = clientFactory;

        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);

        OnMessage("/signin", SignInAsync);
        OnMessage("/signout", SignOutAsync);
        OnMessage("/reset", ResetAsync);
        Authorization.OnUserSignInSuccess(OnUserSignInSuccess);
        Authorization.OnUserSignInFailure(OnUserSignInFailure);

        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    private async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Welcome to AuthenticationBot. Type '/signin' to sign in for OBO.  Type '/signout' to sign-out.  Anything else will be repeated back."), cancellationToken);
            }
        }
    }

    private async Task SignInAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await Authorization.SignInUserAsync(turnContext, turnState, "obo", exchangeScopes: [CopilotClient.ScopeFromSettings(_mcsSettings)], cancellationToken: cancellationToken);
    }

    private async Task SignOutAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await Authorization.SignOutUserAsync(turnContext, turnState, "obo", cancellationToken: cancellationToken);
        await turnContext.SendActivityAsync("You have signed out", cancellationToken: cancellationToken);
    }

    private async Task ResetAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await turnState.Conversation.DeleteStateAsync(turnContext, cancellationToken);
        await turnState.User.DeleteStateAsync(turnContext, cancellationToken);
        await turnContext.SendActivityAsync("Ok I've deleted the current turn state", cancellationToken: cancellationToken);
    }

    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        // Not one of the defined inputs.  Just repeat what user said.
        await turnContext.SendActivityAsync($"You said: {turnContext.Activity.Text}", cancellationToken: cancellationToken);
    }

    private async Task OnUserSignInSuccess(ITurnContext turnContext, ITurnState turnState, string handlerName, string token, IActivity initiatingActivity, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync($"Manual Sign In: Successfully logged in with '{handlerName}'", cancellationToken: cancellationToken);
        if (handlerName == "obo")
        {
            CopilotClient cpsClient = new CopilotClient(
                _mcsSettings,
                _httpClientFactory,
                tokenProviderFunction: (s) =>
                {
                    return Task.FromResult(token);
                },
                NullLogger.Instance,
                "mcs");

            await foreach (Activity act in cpsClient.StartConversationAsync(emitStartConversationEvent: true, cancellationToken: cancellationToken))
            {
                if (act is null)
                {
                    throw new InvalidOperationException("Activity is null");
                }
            }

        }
    }

    private async Task OnUserSignInFailure(ITurnContext turnContext, ITurnState turnState, string handlerName, SignInResponse response, IActivity initiatingActivity, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync($"Manual Sign In: Failed to login with '{handlerName}': {response.Error.Message}", cancellationToken: cancellationToken);
    }
}
