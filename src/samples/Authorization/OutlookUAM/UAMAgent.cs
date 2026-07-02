// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.AdaptiveCards;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core.Models;

namespace OutlookUAM;

public partial class UAMAgent(AgentApplicationOptions options) : AgentApplication(options)
{
    [MessageRoute("-signout")]
    public async Task OnSignOutAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        // "me" is our handler, but it is using the "graph" OAuth Connection
        await UserAuthorization.SignOutUserAsync(turnContext, turnState, "me", cancellationToken);
        await turnContext.SendActivityAsync("You have been signed out", cancellationToken: cancellationToken);
    }

    [MessageRoute("-card")]
    public Task OnCardAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var card = $$"""
            {
              "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
              "type": "AdaptiveCard",
              "version": "1.4",
              "body": [
                {
                  "type": "TextBlock",
                  "text": "Present a form and submit it back to the originator"
                },
                {
                  "type": "Input.Text",
                  "id": "firstName",
                  "placeholder": "What is your first name?"
                },
                {
                  "type": "Input.Text",
                  "id": "lastName",
                  "placeholder": "What is your last name?"
                },
                {
                  "type": "ActionSet",
                  "actions": [
                    {
                      "type": "Action.Execute",
                      "title": "Submit",
                      "verb": "personalDetailsFormSubmit"
                    }
                  ]
                }
              ]
            }
            """;

        return turnContext.SendActivityAsync(MessageFactory.Attachment(new Attachment
        {
            ContentType = ContentTypes.AdaptiveCard,
            Content = card
        }), cancellationToken: cancellationToken);
    }
    
    [ActionExecuteRoute("personalDetailsFormSubmit")]
    private async Task<AdaptiveCardInvokeResponse> ActionExecuteHandler(ITurnContext turnContext, ITurnState turnState, AdaptiveCardInvokeValue invokeValue, CancellationToken cancellationToken)
    {
        var tokenClient = turnContext.Services.Get<IUserTokenClient>();

        // The Action.Execute data can contain an "authentication" object to provide the token
        if (!string.IsNullOrWhiteSpace(invokeValue.Authentication?.Token))
        {
            try
            {
                await tokenClient.ExchangeTokenAsync(turnContext.Activity.From.Id, "graph", turnContext.Activity.ChannelId, new TokenExchangeRequest
                {
                    Token = invokeValue.Authentication.Token
                }, cancellationToken);
            }
            catch (Exception)
            {
                return AdaptiveCardInvokeResponseFactory.BadRequest("Token exchange failed");
            }

            return AdaptiveCardInvokeResponseFactory.Message("Sign in complete");
        }

        // State is the 6-digit code sent by the user.
        if (!string.IsNullOrWhiteSpace(invokeValue.State))
        {
            await tokenClient.GetUserTokenAsync(turnContext.Activity.From.Id, "graph", turnContext.Activity.ChannelId, invokeValue.State, cancellationToken);
            return AdaptiveCardInvokeResponseFactory.Message("Sign in complete");
        }

        // check if the user is already signed in
        var response = await tokenClient.GetTokenOrSignInResourceAsync("graph", turnContext.Activity, null, cancellationToken: cancellationToken);
        if (!string.IsNullOrWhiteSpace(response?.TokenResponse?.Token))
        {
            return AdaptiveCardInvokeResponseFactory.Message("Already signed in");
        }

        // return an OAuthCard in the InvokeResponse
        var oauthCard = new OAuthCard
        {
            Text = "Please sign-in",
            ConnectionName = "graph",
            Buttons =
                    [
                        new CardAction
                        {
                            Title = "Sign In",
                            Text = "Please sign-in",
                            Type = ActionTypes.Signin,
                            Value = response!.SignInResource.SignInLink
                        },
                    ],
            TokenExchangeResource = response!.SignInResource.TokenExchangeResource,
            TokenPostResource = response!.SignInResource.TokenPostResource
        };

        return AdaptiveCardInvokeResponseFactory.Login(oauthCard);
    }
}
