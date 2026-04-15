// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Agents.Extensions.Slack.Api;

namespace SlackAgent;

[Agent(name: "MyAgent", description: "Echo user messages back", version: "1.0")]
public class MyAgent : AgentApplication
{
    public MyAgent(AgentApplicationOptions options) : base(options)
    {
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
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
        var channelData = turnContext.Activity.GetChannelData<SlackChannelData>();
        var slackApi = turnContext.Services.Get<SlackApi>();
        var message = new 
        { 
            channel = channelData.SlackMessage.Event.Channel, 
            text = $"You said: {turnContext.Activity.Text}" 
        };

        await slackApi.CallAsync("chat.postMessage", message, channelData.ApiToken,cancellationToken);
    }
}
