// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.State;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WeatherBot.Agents;

namespace WeatherBot
{
    // This is the core handler for the Bot Message loop. Each new request will be processed by this class.
    public class MyBot : ActivityHandler
    {
        private readonly WeatherForecastAgent _weatherAgent;
        private readonly ConversationState _conversationState;
        private ChatHistory _chatHistory;

        public MyBot(WeatherForecastAgent weatherAgent, ConversationState conversationState)
        {
            _weatherAgent = weatherAgent;
            _conversationState = conversationState;
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            // Invoke the WeatherForecastAgent to process the message
            var forecastResponse = await _weatherAgent.InvokeAgentAsync(turnContext.Activity.Text, _chatHistory);
            if (forecastResponse == null)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Sorry, I couldn't get the weather forecast at the moment."), cancellationToken);
                return;
            }

            // Create a response message based on the response content type from the WeatherForecastAgent
            IActivity response = forecastResponse.ContentType switch
            {
                WeatherForecastAgentResponseContentType.AdaptiveCard => MessageFactory.Attachment(new Attachment()
                {
                    ContentType = "application/vnd.microsoft.card.adaptive",
                    Content = forecastResponse.Content,
                }),
                _ => MessageFactory.Text(forecastResponse.Content),
            };

            // Send the response message back to the user. 
            await turnContext.SendActivityAsync(response, cancellationToken);
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            // When someone (or something) connects to the bot, a MembersAdded activity is received.
            // For this sample,  we treat this as a welcome event, and send a message saying hello.
            // For more details around the membership lifecycle, please see the lifecycle documentation.
            IActivity message = MessageFactory.Text("Hello and Welcome! I'm here to help with all your weather forecast needs!");

            // Send the response message back to the user. 
            await turnContext.SendActivityAsync(message, cancellationToken);
        }

        protected override async Task OnTurnBeginAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await _conversationState.LoadAsync(turnContext, cancellationToken: cancellationToken);
            _chatHistory = await _conversationState.GetPropertyAsync(turnContext, "chatHistory", () => new ChatHistory(), cancellationToken: cancellationToken);
        }

        protected override async Task OnTurnEndAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await _conversationState.SaveChangesAsync(turnContext, cancellationToken: cancellationToken);
        }
    }
}