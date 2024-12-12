// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Teams.Adapter;
using Microsoft.Agents.Protocols.Primitives;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.BotBuilder.Dialogs;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.Dialogs.State;

namespace Microsoft.Agents.Samples.Bots
{
    // This IBot implementation is designed to support multiple Dialog types. Using a type parameter (T),
    // it enables different bot instances to run distinct Dialogs at separate endpoints within the same project.
    // By defining unique Controller types, each dependent on specific IBot types, ASP.NET Core Dependency Injection 
    // can bind them together, avoiding ambiguity.
    // ConversationState manages state across Dialog steps. While UserState is not directly used by the Dialog system,
    // it may be utilized within custom Dialogs. Both ConversationState and UserState need to be saved at the end of each turn.
    public class DialogBot<T> : TeamsActivityHandler where T : Dialog
    {
        protected readonly BotState ConversationState;
        protected readonly Dialog Dialog;
        protected readonly ILogger Logger;
        protected readonly BotState UserState;

        public DialogBot(ConversationState conversationState, UserState userState, T dialog, ILogger<DialogBot<T>> logger)
        {
            ConversationState = conversationState;
            UserState = userState;
            Dialog = dialog;
            Logger = logger;
        }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            await base.OnTurnAsync(turnContext, cancellationToken);

            // Save any changes to ConversationState and UserState made during the turn.
            await ConversationState.SaveChangesAsync(turnContext, false, cancellationToken);
            await UserState.SaveChangesAsync(turnContext, false, cancellationToken);
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            Logger.LogInformation("Running dialog with Message Activity.");

            // Execute the Dialog with the incoming message Activity.
            await Dialog.RunAsync(turnContext, ConversationState.CreateProperty<DialogState>(nameof(DialogState)), cancellationToken);
        }
    }
}
