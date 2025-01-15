// <copyright file="TeamsBot.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.BotBuilder.Dialogs;
using Microsoft.Agents.State;
using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;

namespace BotConversationSsoQuickstart.Bots
{
    // This bot is derived (view DialogBot<T>) from the TeamsActivityHandler class currently included as part of this sample.
    public class TeamsBot<T>(ConversationState conversationState, UserState userState, T dialog, ILogger<DialogBot<T>> logger) 
        : DialogBot<T>(conversationState, userState, dialog, logger) where T : Dialog
    {
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

        protected override async Task OnTeamsSigninVerifyStateAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Running dialog with signin/verifystate from an Invoke Activity.");

            // The OAuth Prompt needs to see the Invoke Activity in order to complete the login process.
            // Run the Dialog with the new Invoke Activity.
            await _dialog.RunAsync(turnContext, _conversationState.CreateProperty<DialogState>(nameof(DialogState)), cancellationToken);
        }
    }
}