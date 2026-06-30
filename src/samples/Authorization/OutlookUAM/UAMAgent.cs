// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.AdaptiveCards;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core.Models;
using System.Text.Json;

namespace OutlookUAM;

public partial class UAMAgent(AgentApplicationOptions options) : AgentApplication(options)
{
    [MessageRoute("-signout")]
    public Task OnSignOutAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        // "me" is our handler, but it is using the "graph" OAuth Connection
        return UserAuthorization.SignOutUserAsync(turnContext, turnState, "me", cancellationToken).ContinueWith(_ => turnContext.SendActivityAsync("You have been signed out", cancellationToken: cancellationToken));
    }

    [AdaptiveCardActionExecuteRoute("personalDetailsFormSubmit")]
    private async Task<AdaptiveCardInvokeResponse> ActionExecuteHandler(ITurnContext turnContext, ITurnState turnState, object data, CancellationToken cancellationToken)
    {
        var tokenClient = turnContext.Services.Get<IUserTokenClient>();

        // The Action.Execute data will contain an "authentication" object to provide the token
        if (data is JsonElement jsonElement && jsonElement.TryGetProperty("authentication", out var authenticationProperty))
        {
            if (authenticationProperty.TryGetProperty("token", out var tokenProperty) && !string.IsNullOrWhiteSpace(tokenProperty.ToString()))
            {
                // should likely exchange with UserTokenClient
                await turnContext.SendActivityAsync("You are signed in", cancellationToken: cancellationToken);
                return AdaptiveCardInvokeResponseFactory.Message("Sign in complete");
            }
        }

        // check if the user is already signed in
        var response = await tokenClient.GetTokenOrSignInResourceAsync("graph", turnContext.Activity, null, cancellationToken: cancellationToken);
        if (!string.IsNullOrWhiteSpace(response?.TokenResponse?.Token))
        {
            return AdaptiveCardInvokeResponseFactory.Message("Already signed in");
        }

        // return an OAuthCard in an login response
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
                            Type = "signin",
                            Value = response!.SignInResource.SignInLink
                        },
                    ],
            TokenExchangeResource = response!.SignInResource.TokenExchangeResource,
            TokenPostResource = response!.SignInResource.TokenPostResource
        };

        return AdaptiveCardInvokeResponseFactory.Login(oauthCard);
    }
}
