// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;

namespace AuthenticationBot
{
    public class AuthBot(IConfiguration configuration, IStorage storage) : ActivityHandlerWithOAuth(storage, new OAuthSettings() { ConnectionName = configuration["ConnectionName"] })
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

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            if (string.Equals("logout", turnContext.Activity.Text, StringComparison.OrdinalIgnoreCase))
            {
                await SignOutUserAsync(turnContext, cancellationToken);
                await turnContext.SendActivityAsync(MessageFactory.Text("You have been signed out."), cancellationToken);
            }
            else
            {
                // This will return a token when the user is signed in, otherwise start the sign in flow.
                var tokenResponse = await SigninUserAsync(turnContext, cancellationToken);
                if (tokenResponse == null)
                {
                    return;
                }

                await turnContext.SendActivityAsync(MessageFactory.Text($"Here is your token {tokenResponse.Token}"), cancellationToken);
            }
        }
    }
}
