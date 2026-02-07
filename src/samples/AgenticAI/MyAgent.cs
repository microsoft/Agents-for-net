// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.Builders;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticAI;

public class MyAgent : AgentApplication
{
    public MyAgent(AgentApplicationOptions options) : base(options)
    {
        // (WITH BUILDER) Register a route for any channel including Agentic, with a dynamic autoSignInHandler list.
        var route = MessageRouteBuilder.Create()
            .WithText("-me")
            .WithHander(OnMeAsync)
            .WithOAuthHandlers(OAuthHandlers)
            .Build();
        AddRoute(route);

        // (COMPAT) Register a route for Agentic-only Messages.
        OnMessage("-agentic", OnAgenticMessageAsync, isAgenticOnly: true, autoSignInHandlers: ["agentic"]);

        // (COMPAT) Non-agentic messages go here
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    private static string[] OAuthHandlers(ITurnContext turnContext)
    {
        if (turnContext.Activity.IsAgenticRequest())
        {
            return ["agentic"];
        }
        return ["bot"];
    }

    private async Task OnAgenticMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var aauToken = await turnContext.GetTurnTokenAsync("agentic", cancellationToken);
        await turnContext.SendActivityAsync($"(Agentic Only) You said: {turnContext.Activity.Text}, user token len={aauToken.Length}", cancellationToken: cancellationToken);
    }

    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync($"(No OAuth) You said: {turnContext.Activity.Text}", cancellationToken: cancellationToken);
    }

    private async Task OnMeAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var tokens = turnContext.GetTurnTokens();
        await turnContext.SendActivityAsync($"({tokens[0].Handler}) You said: {turnContext.Activity.Text}, token len={tokens[0].Token.Length}", cancellationToken: cancellationToken);
    }
}
