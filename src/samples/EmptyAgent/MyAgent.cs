// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Agents.Core.Models.Activities;

namespace EmptyAgent;

public class MyAgent(AgentApplicationOptions options) : AgentApplication(options)
{
    [ConversationUpdate(Event = ConversationUpdateEvents.MembersAdded)]
    public async Task WelcomeMessageAsync(ITurnContext<IConversationUpdateActivity> turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(new MessageActivity("Hello and Welcome!"), cancellationToken);
            }
        }
    }

    [Message]
    public async Task OnMessageAsync(ITurnContext<IMessageActivity> turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync($"You said: {turnContext.Activity.Text}", cancellationToken: cancellationToken);
    }
}
