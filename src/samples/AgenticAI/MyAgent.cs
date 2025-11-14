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
        // Register a route for Agentic-only Messages.
        OnActivity(ActivityTypes.Message, OnAgenticMessageAsync, isAgenticOnly: true, autoSignInHandlers: ["agentic", "me"]);

        // Non-agentic messages go here
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last, autoSignInHandlers: ["me"]);
    }

    private async Task OnAgenticMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var aauToken = await UserAuthorization.GetTurnTokenAsync(turnContext, "agentic", cancellationToken);
        var meToken = await UserAuthorization.GetTurnTokenAsync(turnContext, "me", cancellationToken);
        await turnContext.SendActivityAsync($"(Agentic) You said: {turnContext.Activity.Text}, AAU token len={aauToken.Length}, Me token len={meToken.Length}", cancellationToken: cancellationToken);
    }

    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync($"You said: {turnContext.Activity.Text}", cancellationToken: cancellationToken);
    }
}
