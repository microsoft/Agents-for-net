// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace TeamsSSOBot
{
    public class AuthBot : AgentApplication
    {
        public AuthBot(AgentApplicationOptions options) : base(options)
        {
            Authentication.OnUserSignInSuccess(async (turnContext, turnState, flowName, tokenResponse, cancellationToken) =>
            {
                await turnContext.SendActivityAsync($"Successfully logged in to '{flowName}'", cancellationToken: cancellationToken);
            });

            Authentication.OnUserSignInFailure(async (turnContext, turnState, flowName, response, cancellationToken) =>
            {
                await turnContext.SendActivityAsync($"Failed to login to '{flowName}': {response.Error.Message}", cancellationToken: cancellationToken);
            });

            OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);

            OnMessage("/reset", ResetAsync);
            OnMessage("/signin", SignInAsync);
            OnMessage("/signout", SignOutAsync);

            // Listen for ANY message to be received. MUST BE AFTER ANY OTHER MESSAGE HANDLERS
            OnActivity(ActivityTypes.Message, OnMessageAsync);
        }

        protected async Task SignInAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            await Authentication.GetTokenOrStartSignInAsync(turnContext, turnState, "graph", cancellationToken);
        }

        protected async Task SignOutAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            await Authentication.SignOutUserAsync(turnContext, turnState, cancellationToken: cancellationToken);
            await turnContext.SendActivityAsync("You have signed out", cancellationToken: cancellationToken);
        }

        protected async Task ResetAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            await turnState.Conversation.DeleteStateAsync(turnContext, cancellationToken);
            await turnState.User.DeleteStateAsync(turnContext, cancellationToken);
            await turnContext.SendActivityAsync("Ok I've deleted the current turn state", cancellationToken: cancellationToken);
        }

        protected async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text("Welcome to AuthenticationBot. Type 'auto' to demonstrate Auto SignIn. Type '/signin' to sign in for graph.  Type '/signout' to sign-out.  Anything else will be repeated back."), cancellationToken);
                }
            }
        }

        protected async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.Text == "auto")
            {
                await turnContext.SendActivityAsync($"Auto Sign In: Successfully logged in to '{Authentication.Default}', token length: {turnState.Temp.AuthTokens[Authentication.Default].Length}", cancellationToken: cancellationToken);
            }
            else
            {
                await turnContext.SendActivityAsync($"You said: {turnContext.Activity.Text}", cancellationToken: cancellationToken);
            }
        }
    }
}
