// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Proactive;

public class ProactiveAgent : AgentApplication
{
    public ProactiveAgent(AgentApplicationOptions options) : base(options)
    {
        // Manual way to store a conversation for use in Proactive.  This is for sample purposes only.
        OnMessage("-s", async (turnContext, turnState, cancellationToken) =>
        {
            var id = await Proactive.StoreConversationAsync(turnContext, cancellationToken);
            await turnContext.SendActivityAsync($"Conversation '{id}' stored", cancellationToken: cancellationToken);
        });

        OnMessage("-signin", async (turnContext, turnState, cancellationToken) =>
        {
            await turnContext.SendActivityAsync("Signed in", cancellationToken: cancellationToken);
        }, autoSignInHandlers: ["me"]);

        OnMessage("-signout", async (turnContext, turnState, cancellationToken) =>
        {
            await UserAuthorization.SignOutUserAsync(turnContext, turnState, "me", cancellationToken);
            await turnContext.SendActivityAsync("Signed out", cancellationToken: cancellationToken);
        });

        // In-code ContinueConversation 
        OnMessage(new Regex("-c.*"), async (turnContext, turnState, cancellationToken) =>
        {
            var split = turnContext.Activity.Text.Split(' ');
            var conversationId = split.Length == 1 ? turnContext.Activity.Conversation.Id : split[1];

            await Proactive.ContinueConversationAsync(turnContext.Adapter, conversationId, OnContinueConversationAsync, cancellationToken: cancellationToken);
        });
    }

    [Route(RouteType = RouteType.Conversation, EventName = ConversationUpdateEvents.MembersAdded)]
    public async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Hello and Welcome!"), cancellationToken);
            }
        }
    }

    [Route(Type = ActivityTypes.Message, Rank = RouteRank.Last)]
    public async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync($"You said: {turnContext.Activity.Text}", cancellationToken: cancellationToken);
    }

    // Map /proactive/continue to this method
    [ContinueConversation(tokenHandlers: "me")]
    public async Task OnContinueConversationAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var token = await UserAuthorization.GetTurnTokenAsync(turnContext, "me", cancellationToken); 
        await turnContext.SendActivityAsync($"This is OnContinueConversation. Token={(token == null ? "not signed in" : token.Length)}, Activity.Value={JsonSerializer.Serialize(turnContext.Activity.Value)}", cancellationToken: cancellationToken);
    }

    // Map /proactive/continue/ext to this method
    [ContinueConversation("ext")]
    public Task OnContinueConversationExtendedAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        return turnContext.SendActivityAsync($"This is ContinueConversationExtended. Activity.Value={JsonSerializer.Serialize(turnContext.Activity.Value)}", cancellationToken: cancellationToken);
    }
}
