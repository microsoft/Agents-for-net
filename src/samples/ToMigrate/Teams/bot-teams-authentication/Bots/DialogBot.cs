// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.BotBuilder.Dialogs;
using Microsoft.Agents.Extensions.Teams.Compat;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.BotBuilder;

namespace TeamsAuth.Bots
{
    // This IBot implementation is designed to support multiple Dialog types. Using a type parameter (T),
    // it enables different bot instances to run distinct Dialogs at separate endpoints within the same project.
    // By defining unique Controller types, each dependent on specific IBot types, ASP.NET Core Dependency Injection 
    // can bind them together, avoiding ambiguity.
    // ConversationState manages state across Dialog steps. While UserState is not directly used by the Dialog system,
    // it may be utilized within custom Dialogs. Both ConversationState and UserState need to be saved at the end of each turn.
    public class DialogBot<T>(ConversationState conversationState, UserState userState, T dialog, ILogger<DialogBot<T>> logger) : TeamsActivityHandler where T : Dialog
    {
        protected readonly BotState ConversationState = conversationState;
        protected readonly Dialog Dialog = dialog;
        protected readonly ILogger Logger = logger;
        protected readonly BotState UserState = userState;

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            Logger.LogInformation("Running dialog with Message Activity.");

            // Execute the Dialog with the incoming message Activity.
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
