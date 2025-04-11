// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.AI;

namespace StreamingMessageAgent;

public class StreamingAgent : AgentApplication
{
    private readonly IChatClient _chatClient;

    public StreamingAgent(AgentApplicationOptions options, IChatClient chatClient) : base(options)
    {
        _chatClient = chatClient;

        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    private async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Say anything and I'll recite poetry."), cancellationToken);
            }
        }
    }

    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        try
        {
            await turnContext.StreamingResponse.QueueInformativeUpdateAsync("Hold on for an awesome poem...", cancellationToken);

            await foreach (StreamingChatCompletionUpdate update in _chatClient.CompleteStreamingAsync(
                "Write a poem about why Microsoft Agents SDK is so great.",
                new ChatOptions { MaxOutputTokens = 1000 },
                cancellationToken: cancellationToken))
            {
                turnContext.StreamingResponse.QueueTextChunk(update?.ToString());
            }
        }
        finally
        {
            await turnContext.StreamingResponse.EndStreamAsync(cancellationToken);
        }
    }
}
