// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System.Threading.Tasks;
using System.Threading;

namespace A2AAgent;

public class MyAgent : AgentApplication
{
    public MyAgent(AgentApplicationOptions options) : base(options)
    {
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
        OnMessage("-stream", OnStreamAsync);
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

    private async Task OnStreamAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await turnContext.StreamingResponse.QueueInformativeUpdateAsync("Informative", cancellationToken);
        turnContext.StreamingResponse.QueueTextChunk("a quick");
        await Task.Delay(600);
        turnContext.StreamingResponse.QueueTextChunk("a quick a quick brown fox ");
        await Task.Delay(600);
        turnContext.StreamingResponse.QueueTextChunk("a quick a quick brown fox jumped over something");
        await Task.Delay(600);
        await turnContext.StreamingResponse.EndStreamAsync(cancellationToken);
    }

    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var activity = MessageFactory.Text($"You said: {turnContext.Activity.Text}");
        activity.Entities = [new StreamInfo() { StreamId = "streamId" }];

        await turnContext.SendActivityAsync(activity, cancellationToken: cancellationToken);
    }
}
