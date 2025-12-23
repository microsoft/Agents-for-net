// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI;

public class MyAgent : AgentApplication
{
    public MyAgent(AgentApplicationOptions options) : base(options)
    {
        // Register a route for any channel including Agentic, with a dynamic autoSignInHandler list.
        OnMessage("-me", OnMeAsync, autoSignInHandlers: (SignInResolver)SignInDelegate);

        // Register a route for Agentic-only Messages.
        OnMessage("-agentic", OnAgenticMessageAsync, isAgenticOnly: true, autoSignInHandlers: ["agentic"]);

        // Non-agentic messages go here
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    private static string[] SignInDelegate(ITurnContext turnContext)
    {
        if (turnContext.Activity.IsAgenticRequest())
        {
            return new string[] { "agentic" };
        }
        return ["bot"];
    }

    private async Task OnAgenticMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var aauToken = await UserAuthorization.GetTurnTokenAsync(turnContext, "agentic", cancellationToken);
        await turnContext.SendActivityAsync($"(Agentic Only) You said: {turnContext.Activity.Text}, user token len={aauToken.Length}", cancellationToken: cancellationToken);
    }

    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync($"(No OAuth) You said: {turnContext.Activity.Text}", cancellationToken: cancellationToken);
    }

    private async Task OnMeAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var token = await turnContext.GetTurnTokenAsync(cancellationToken: cancellationToken);
        await turnContext.SendActivityAsync($"({(turnContext.Activity.IsAgenticRequest() ? "Agentic" : "Bot")}) You said: {turnContext.Activity.Text}, user token len={token.Length}", cancellationToken: cancellationToken);
    }
}
