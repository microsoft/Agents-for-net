// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace TeamsSSOBot
{
    public class AuthBot : AgentApplication
    {
        public AuthBot(AgentApplicationOptions options) : base(options)
        {
            UserAuthorization.OnUserSignInFailure(async (turnContext, turnState, flowName, response, cancellationToken) =>
            {
                await turnContext.SendActivityAsync($"Failed to login to '{flowName}': {response.Error.Message}", cancellationToken: cancellationToken);
            });

            // Listen for ANY message to be received. MUST BE AFTER ANY OTHER MESSAGE HANDLERS
            OnActivity(ActivityTypes.Message, OnMessageAsync);
        }

        protected async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            await turnContext.SendActivityAsync($"Auto Sign In: Successfully logged in to '{UserAuthorization.DefaultHandlerName}', token length: {UserAuthorization.GetTurnToken(UserAuthorization.DefaultHandlerName).Length}", cancellationToken: cancellationToken);
        }
    }
}
