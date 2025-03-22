// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Threading;
using System.Threading.Tasks;
using WeatherAgent.Agents;

namespace WeatherAgent;

// This is the core handler for the Bot Message loop. Each new request will be processed by this class.
public class Weather(AgentApplicationOptions options, WeatherForecastAgent weatherAgent) : AgentApplication(options)
{
    [Route(RouteType = RouteType.Activity, Type = ActivityTypes.Message, Rank = RouteRank.Last)]
    protected async Task MessageActivityAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var chatHistory = turnState.GetValue("conversation.chatHistory", () => new ChatHistory());

        // Invoke the WeatherForecastAgent to process the message
        var forecastResponse = await weatherAgent.InvokeAgentAsync(turnContext.Activity.Text, chatHistory);
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

    [Route(RouteType = RouteType.Conversation, EventName = ConversationUpdateEvents.MembersAdded)]
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