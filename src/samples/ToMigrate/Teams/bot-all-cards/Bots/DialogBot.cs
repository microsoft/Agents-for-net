// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.BotBuilder.Dialogs;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.BotBuilder.Compat;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.State;

namespace BotAllCards.Bots
{
    // This IBot implementation can run any type of Dialog. The use of type parameterization is to allows multiple different bots
    // to be run at different endpoints within the same project. This can be achieved by defining distinct Controller types
    // each with dependency on distinct IBot types, this way ASP Dependency Injection can glue everything together without ambiguity.
    // The ConversationState is used by the Dialog system. The UserState isn't, however, it might have been used in a Dialog implementation,
    // and the requirement is that all BotState objects are saved at the end of a turn.
    public class DialogBot<T>(ConversationState conversationState, UserState userState, T dialog, ILogger<DialogBot<T>> logger) : ActivityHandler where T : Dialog
    {
        protected readonly BotState ConversationState = conversationState;
        protected readonly Dialog Dialog = dialog;
        protected readonly ILogger Logger = logger;
        protected readonly BotState UserState = userState;

        /// <summary>
        /// Handle when a message is addressed to the bot.
        /// </summary>
        /// <param name="turnContext">Context object containing information cached for a single turn of conversation with a user.</param>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        /// <returns>A Task resolving to either a login card or the adaptive card of the Reddit post.</returns>
        /// <remarks>
        /// For more information on bot messaging in Teams, see the documentation
        /// https://docs.microsoft.com/en-us/microsoftteams/platform/bots/how-to/conversations/conversation-basics?tabs=dotnet#receive-a-message .
        /// </remarks>
        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            Logger.LogInformation("Running dialog with Message Activity.");

            // Run the Dialog with the new message Activity.
            await Dialog.RunAsync(turnContext, ConversationState, cancellationToken);
        }

        protected override async Task OnTurnBeginAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await ConversationState.LoadAsync(turnContext, false, cancellationToken);
            await UserState.LoadAsync(turnContext, false, cancellationToken);
        }

        protected override async Task OnTurnEndAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            // Save any state changes that might have occurred during the turn.
            await ConversationState.SaveChangesAsync(turnContext, false, cancellationToken);
            await UserState.SaveChangesAsync(turnContext, false, cancellationToken);
        }
    }
}
