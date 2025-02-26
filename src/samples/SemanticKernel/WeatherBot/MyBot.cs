// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Threading;
using System.Threading.Tasks;
using WeatherBot.Agents;

namespace WeatherBot
{
    // This is the core handler for the Bot Message loop. Each new request will be processed by this class.
    public class MyBot : Application
    {
        private readonly WeatherForecastAgent _weatherAgent;

        public MyBot(ApplicationOptions options, WeatherForecastAgent weatherAgent) : base(options)
        {
            _weatherAgent = weatherAgent;

            // Setup Activity routes
            OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);

            // Listen for ANY message to be received. MUST BE AFTER ANY OTHER MESSAGE HANDLERS
            OnActivity(ActivityTypes.Message, MessageActivityAsync);
        }

        protected async Task MessageActivityAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            var chatHistory = turnState.GetValue("conversation.chatHistory", () => new ChatHistory());

            // Invoke the WeatherForecastAgent to process the message
            var forecastResponse = await _weatherAgent.InvokeAgentAsync(turnContext.Activity.Text, chatHistory);
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

        protected async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text("Hello and Welcome! I'm here to help with all your weather forecast needs!"), cancellationToken);
                }
            }
        }
    }
}