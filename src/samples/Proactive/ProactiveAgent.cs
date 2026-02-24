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
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);

        // Manual way to store a conversation for use in Proactive.  This is for sample purposes only.
        OnMessage("-s", async (turnContext, turnState, cancellationToken) =>
        {
            var id = await Proactive.StoreConversationAsync(turnContext, cancellationToken);
            await turnContext.SendActivityAsync($"Conversation '{id}' stored", cancellationToken: cancellationToken);
        });

        // In-code ContinueConversation 
        OnMessage(new Regex("-c.*"), async (turnContext, turnState, cancellationToken) =>
        {
            var split = turnContext.Activity.Text.Split(' ');
            var conversationId = split.Length == 1 ? turnContext.Activity.Conversation.Id : split[1];

            await Proactive.ContinueConversationAsync(turnContext.Adapter, conversationId, OnContinueConversationAsync, cancellationToken: cancellationToken);
        });

        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    private async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Hello and Welcome!"), cancellationToken);
            }
        }
    }

    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync($"You said: {turnContext.Activity.Text}", cancellationToken: cancellationToken);
    }

    [ContinueConversation]
    public async Task OnContinueConversationAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var token = await UserAuthorization.GetTurnTokenAsync(turnContext, cancellationToken: cancellationToken);
        await turnContext.SendActivityAsync($"This is ContinueConversation with token len={token?.Length}. Value={JsonSerializer.Serialize(turnContext.Activity.Value)}", cancellationToken: cancellationToken);
    }

    [ContinueConversation("ext")]
    public async Task OnContinueConversationExtendedAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync($"This is ContinueConversationExtended. Value={JsonSerializer.Serialize(turnContext.Activity.Value)}", cancellationToken: cancellationToken);
    }
}
