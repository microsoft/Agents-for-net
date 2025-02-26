// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.BotBuilder.Dialogs;
using Microsoft.Extensions.Configuration;

namespace TeamsAuth.Dialogs
{
    // MainDialog class handles the user authentication process using an OAuth prompt
    // and provides a sample flow for signing in, retrieving, and displaying user information.
    public class MainDialog : LogoutDialog
    {
        protected readonly ILogger Logger;

        public MainDialog(IConfiguration configuration, ILogger<MainDialog> logger)
            : base(nameof(MainDialog), configuration["ConnectionName"])
        {
            Logger = logger;

            // Define OAuthPrompt to request user sign-in.
            AddDialog(new OAuthPrompt(
                nameof(OAuthPrompt),
                new OAuthPromptSettings
                {
                    ConnectionName = ConnectionName,
                    Text = "Please Sign In",
                    Title = "Sign In",
                    Timeout = 300000, // 5-minute timeout for login (1000 ms * 60 * 5)
                    EndOnInvalidMessage = true
                }));

            // Add a confirmation prompt to ask for user consent before showing the token.
            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));

            // Define the main dialog flow with a waterfall of steps.
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                PromptStepAsync,
                LoginStepAsync,
                DisplayTokenPhase1Async,
                DisplayTokenPhase2Async,
            }));

            // Set the initial child dialog to run when MainDialog starts.
            InitialDialogId = nameof(WaterfallDialog);
        }

        // Step 1: Starts the OAuth prompt to request user sign-in.
        private async Task<DialogTurnResult> PromptStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.BeginDialogAsync(nameof(OAuthPrompt), null, cancellationToken);
        }

        // Step 2: Receives the authentication token from the OAuth prompt and retrieves user information.
        private async Task<DialogTurnResult> LoginStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // Retrieves the token response.
            var tokenResponse = (TokenResponse)stepContext.Result;
            if (tokenResponse?.Token != null)
            {
                // Uses the token to access Microsoft Graph and retrieve user data.
                var client = new SimpleGraphClient(tokenResponse.Token);
                var me = await client.GetMeAsync();
                var title = !string.IsNullOrEmpty(me.JobTitle) ? 
                    me.JobTitle : "Unknown";

                await stepContext.Context.SendActivityAsync($"You're logged in as {me.DisplayName} ({me.UserPrincipalName}); your job title is: {title}");

                // Ask if the user wants to view their token.
                return await stepContext.PromptAsync(nameof(ConfirmPrompt), new PromptOptions { Prompt = MessageFactory.Text("Would you like to view your token?") }, cancellationToken);
            }

            // If login was unsuccessful, send a message and end the dialog.
            await stepContext.Context.SendActivityAsync(MessageFactory.Text("Login was not successful, please try again."), cancellationToken);
            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }

        // Step 3: If user consents, re-prompt for the token to ensure it is up-to-date before displaying.
        private async Task<DialogTurnResult> DisplayTokenPhase1Async(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            await stepContext.Context.SendActivityAsync(MessageFactory.Text("Thank you."), cancellationToken);

            var result = (bool)stepContext.Result;
            if (result)
            {
                // Re-prompts to retrieve a valid token, which ensures the bot does not store tokens locally.
                return await stepContext.BeginDialogAsync(nameof(OAuthPrompt), cancellationToken: cancellationToken);
            }

            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }

        // Step 4: Displays the token if retrieved successfully from the previous step.
        private async Task<DialogTurnResult> DisplayTokenPhase2Async(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var tokenResponse = (TokenResponse)stepContext.Result;
            if (tokenResponse != null)
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Here is your token: {tokenResponse.Token}"), cancellationToken);
            }

            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }
    }
}
