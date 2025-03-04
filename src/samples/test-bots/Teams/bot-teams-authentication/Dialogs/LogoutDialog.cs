﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.BotBuilder.Dialogs;
using Microsoft.Agents.Connector;

namespace TeamsAuth.Dialogs
{
    // LogoutDialog class provides a custom dialog that supports user logout functionality.
    // It allows users to trigger a logout action by sending a "logout" command at any time during a conversation.
    public class LogoutDialog(string id, string connectionName) : ComponentDialog(id)
    {
        // Holds the OAuth connection name used for authentication.
        protected string ConnectionName { get; } = connectionName;

        // This method starts the dialog, checking if a logout interruption is triggered.
        // If the logout command is detected, it processes the logout and cancels any active dialogs.
        protected override async Task<DialogTurnResult> OnBeginDialogAsync(DialogContext innerDc, object options, CancellationToken cancellationToken = default(CancellationToken))
        {
            var result = await InterruptAsync(innerDc, cancellationToken);
            if (result != null)
            {
                return result;
            }

            return await base.OnBeginDialogAsync(innerDc, options, cancellationToken);
        }

        // This method continues the dialog if already started, also checking for a logout interruption.
        protected override async Task<DialogTurnResult> OnContinueDialogAsync(DialogContext innerDc, CancellationToken cancellationToken = default(CancellationToken))
        {
            var result = await InterruptAsync(innerDc, cancellationToken);
            if (result != null)
            {
                return result;
            }

            return await base.OnContinueDialogAsync(innerDc, cancellationToken);
        }

        // Checks if the user sent a "logout" command, and if so, performs the logout process.
        // It signs out the user using the specified OAuth connection and sends a confirmation message.
        private async Task<DialogTurnResult> InterruptAsync(DialogContext innerDc, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (innerDc.Context.Activity.Type == ActivityTypes.Message)
            {
                var text = innerDc.Context.Activity.Text.ToLowerInvariant();

                // Triggers logout if the word "logout" appears in the message.
                if (text.IndexOf("logout") >= 0)
                {
                    // Accesses the UserTokenClient to handle the sign-out process.
                    var userTokenClient = innerDc.Context.Services.Get<IUserTokenClient>();
                    await userTokenClient.SignOutUserAsync(innerDc.Context.Activity.From.Id, ConnectionName, innerDc.Context.Activity.ChannelId, cancellationToken).ConfigureAwait(false);

                    // Sends a confirmation message and cancels any active dialogs.
                    await innerDc.Context.SendActivityAsync(MessageFactory.Text("You have been signed out."), cancellationToken);
                    return await innerDc.CancelAllDialogsAsync(cancellationToken);
                }
            }

            return null;
        }
    }
}
