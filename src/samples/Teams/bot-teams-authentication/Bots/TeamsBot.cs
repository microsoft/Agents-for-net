// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.BotBuilder.Dialogs;
using Microsoft.Agents.State;
using Microsoft.Agents.Core.Interfaces;

namespace TeamsAuth.Bots
{
    // This bot extends the DialogBot<T> class to handle Teams-specific activities.
    // It inherits from TeamsActivityHandler, making it capable of responding to various Microsoft Teams events.
    public class TeamsBot<T> : DialogBot<T> where T : Dialog
    {
        public TeamsBot(ConversationState conversationState, UserState userState, T dialog, ILogger<DialogBot<T>> logger)
            : base(conversationState, userState, dialog, logger)
        {
        }

        // This method is called when new members are added to the conversation.
        // Welcomes each new user with an introductory message guiding them to authenticate or logout.
        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            foreach (var member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text("Welcome to AuthenticationBot. Type anything to get logged in. Type 'logout' to sign-out."), cancellationToken);
                }
            }
        }

        // This method handles the 'signin/verifyState' event, which occurs when users attempt to complete authentication
        // through Microsoft Teams. It runs the dialog in response to this event, allowing the bot to process authentication 
        // state verification through an OAuth Prompt in the dialog.
        protected override async Task OnTeamsSigninVerifyStateAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
        {
            Logger.LogInformation("Running dialog with signin/verifystate from an Invoke Activity.");

            // Run the Dialog with the current Invoke Activity to complete the authentication process.
            await Dialog.RunAsync(turnContext, ConversationState.CreateProperty<DialogState>(nameof(DialogState)), cancellationToken);
        }
    }
}
