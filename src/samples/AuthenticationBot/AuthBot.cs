// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.Compat;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AuthenticationBot
{
    public class AuthBot : ActivityHandler
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly OAuthFlow _flow;
        private readonly PrivateConversationState _botState;

        public AuthBot(IConfiguration configuration, PrivateConversationState conversationState, ILogger<AuthBot> logger)
        {
            _logger = logger ?? NullLogger<AuthBot>.Instance;
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _botState = conversationState ?? throw new ArgumentNullException(nameof(conversationState));
            _flow = new OAuthFlow("Sign In", "Please sign in", _configuration["ConnectionName"], 30000, null);
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            // Display a welcome message
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
                _logger.LogInformation("User signing out");

                await _flow.SignOutUserAsync(turnContext, cancellationToken);
                await turnContext.SendActivityAsync(MessageFactory.Text("You have been signed out."), cancellationToken);
            }
            else
            {
                TokenResponse tokenResponse;

                var state = _botState.GetValue("flowState", () => new FlowState());
                if (!state.FlowStarted)
                {
                    tokenResponse = await _flow.BeginFlowAsync(turnContext, null, cancellationToken);

                    // If a TokenResponse is returned, there was a cached token already.  Otherwise, start the process of getting a new token.
                    if (tokenResponse == null)
                    {
                        var expires = DateTime.UtcNow.AddMilliseconds(_flow.Timeout ?? TimeSpan.FromMinutes(15).TotalMilliseconds);

                        state.FlowStarted = true;
                        state.FlowExpires = expires;
                    }
                    else
                    {
                        _logger.LogInformation("User is already signed in");
                        await turnContext.SendActivityAsync(MessageFactory.Text("You are still logged in."), cancellationToken);
                    }
                }
                else
                {
                    _logger.LogInformation("Exchange user magic code for a token");

                    // For non-Teams bots, the user sends the "magic code" that will be used to exchange for a token.
                    tokenResponse = await OnContinueFlow(turnContext, cancellationToken);
                }

                if (tokenResponse != null)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text($"Here is your token {tokenResponse.Token}"), cancellationToken);
                }
            }
        }

        protected override Task OnTokenResponseEventAsync(ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Response Event Activity (Not handled).");
            return Task.CompletedTask;
        }

        protected override async Task OnSignInInvokeAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Exchange Teams code for a token");

            // Teams will send the bot an "Invoke" Activity that contains a value that will be exchanged for a token.
            await OnContinueFlow(turnContext, cancellationToken);
        }

        private async Task<TokenResponse> OnContinueFlow(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            TokenResponse tokenResponse = null;

            var state = _botState.GetValue("flowState", () => new FlowState());

            try
            {
                tokenResponse = await _flow.ContinueFlowAsync(turnContext, state.FlowExpires, cancellationToken);
                if (tokenResponse != null)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text("You are now logged in."), cancellationToken);
                }
                else
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text("Login was not successful please try again."), cancellationToken);
                }
            }
            catch (TimeoutException)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("You did not respond in time.  Please try again."), cancellationToken);
            }

            state.FlowStarted = false;
            return tokenResponse;
        }
    }

    class FlowState
    {
        public bool FlowStarted = false;
        public DateTime FlowExpires = DateTime.MinValue;
    }
}
