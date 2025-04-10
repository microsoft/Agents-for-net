﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace AuthorizationAgent;

public class AuthAgent : AgentApplication
{
    public AuthAgent(AgentApplicationOptions options) : base(options)
    {
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);

        OnMessage("/signin", SignInAsync);
        OnMessage("/signout", SignOutAsync);
        OnMessage("/reset", ResetAsync);
        UserAuthorization.OnUserSignInSuccess(OnUserSignInSuccess);
        UserAuthorization.OnUserSignInFailure(OnUserSignInFailure);

        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    private async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Welcome to AuthorizationAgent. Type 'auto' to demonstrate Auto SignIn. Type '/signin' to sign in for graph.  Type '/signout' to sign-out.  Anything else will be repeated back."), cancellationToken);
            }
        }
    }

    private async Task SignInAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await UserAuthorization.SignInUserAsync(turnContext, turnState, "graph", cancellationToken: cancellationToken);
    }

    private async Task SignOutAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await UserAuthorization.SignOutUserAsync(turnContext, turnState, cancellationToken: cancellationToken);
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
        if (turnContext.Activity.Text == "auto")
        {
            await turnContext.SendActivityAsync($"Auto Sign In: Successfully logged in to '{UserAuthorization.DefaultHandlerName}', token length: {UserAuthorization.GetTurnToken(UserAuthorization.DefaultHandlerName).Length}", cancellationToken: cancellationToken);
        }
        else
        {
            // Not one of the defined inputs.  Just repeat what user said.
            await turnContext.SendActivityAsync($"You said: {turnContext.Activity.Text}", cancellationToken: cancellationToken);
        }
    }

    private async Task OnUserSignInSuccess(ITurnContext turnContext, ITurnState turnState, string handlerName, string token, IActivity initiatingActivity, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync($"Manual Sign In: Successfully logged in to '{handlerName}'", cancellationToken: cancellationToken);
    }

    private async Task OnUserSignInFailure(ITurnContext turnContext, ITurnState turnState, string handlerName, SignInResponse response, IActivity initiatingActivity, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync($"Manual Sign In: Failed to login to '{handlerName}': {response.Error.Message}", cancellationToken: cancellationToken);
    }
}
