// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams.App;
using System.Threading.Tasks;
using System.Threading;

namespace HeaderPropagation;

public class MyAgent : AgentApplication
{
    public MyAgent(AgentApplicationOptions options) : base(options)
    {
        RegisterExtension(new TeamsAgentExtension(this), (ext) =>
        {
            ext.OnMessageEdit(OnMessageEditAsync);
        });
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    private async Task OnMessageEditAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var prevActivity = turnState.Conversation.GetActivity(turnContext.Activity.Id);

        await turnContext.SendActivityAsync($"Activity '{prevActivity.Id}' modified from '{prevActivity.Text}' to '{turnContext.Activity.Text}'", cancellationToken: cancellationToken);
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
        turnState.Conversation.SaveActivity(turnContext.Activity);

        await turnContext.SendActivityAsync($"You said: {turnContext.Activity.Text}", cancellationToken: cancellationToken);
    }
}
