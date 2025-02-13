// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.Compat;
using Microsoft.Agents.Core.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CopilotStudioEchoSkill
{
    public class CopilotStudioBot : ActivityHandler
    {
        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.Text.Contains("end") || turnContext.Activity.Text.Contains("stop"))
            {
                var messageText = $"(EchoSkill) Ending conversation...";
                await turnContext.SendActivityAsync(MessageFactory.Text(messageText, messageText, InputHints.IgnoringInput.ToString()), cancellationToken);

                // Indicate this conversation is over by sending an EndOfConversation Activity.
                // This bot doesn't return a value, but if it did it could be put in Activity.Value.
                var endOfConversation = Activity.CreateEndOfConversationActivity();
                endOfConversation.Code = EndOfConversationCodes.CompletedSuccessfully;
                await turnContext.SendActivityAsync(endOfConversation, cancellationToken);
            }
            else
            {
                var messageText = $"Echo(EchoSkill): {turnContext.Activity.Text}";
                await turnContext.SendActivityAsync(MessageFactory.Text(messageText, messageText, InputHints.IgnoringInput.ToString()), cancellationToken);
                messageText = "Echo(EchoSkill): Say \"end\" or \"stop\" and I'll end the conversation and return to the parent.";
                await turnContext.SendActivityAsync(MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput.ToString()), cancellationToken);
            }
        }

        protected override Task OnEndOfConversationActivityAsync(ITurnContext<IEndOfConversationActivity> turnContext, CancellationToken cancellationToken)
        {
            // This will be called if the root bot is ending the conversation.  Sending additional messages should be
            // avoided as the conversation may have been deleted.
            // Perform cleanup of resources if needed.
            return Task.CompletedTask;
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            // Display a welcome message to the new members.
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text("Hi, This is EchoSkill"), cancellationToken);
                }
            }
        }
    }
}
