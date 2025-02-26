// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;
using AdaptiveCards.Templating;
using Microsoft.Agents.Extensions.Teams.Compat;
using Microsoft.Agents.BotBuilder;

namespace AdaptiveCardActions.Bots
{
    // This bot responds to user input with suggested actions.
    // Suggested actions allow your bot to present buttons that the user
    // can tap to provide input. 
    public class AdaptiveCardActionsBot : TeamsActivityHandler
    {
        public string commandString = "Please use one of these commands: **Card Actions** for Adaptive Card actions, **Suggested Actions** for bot suggested actions, and **ToggleVisibility** for toggling the visibility of the card.";

        /// <summary>
        /// Provides logic for when members other than the bot join the conversation.
        /// </summary>
        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            await turnContext.SendActivityAsync(MessageFactory.Text(commandString), cancellationToken);
        }

        /// <summary>
        /// Provides logic specific to Message activities.
        /// </summary>
        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.Text != null)
            {
                // Extract the text from the message activity sent by the user.
                var text = turnContext.Activity.Text.ToLowerInvariant();

                if (text.Contains("card actions"))
                {
                    string[] path = { ".", "Cards", "AdaptiveCardActions.json" };
                    var adaptiveCardForPersonalScope = GetFirstOptionsAdaptiveCard(path, turnContext.Activity.From.Name);
                    await turnContext.SendActivityAsync(MessageFactory.Attachment(adaptiveCardForPersonalScope), cancellationToken);
                }
                else if (text.Contains("suggested actions"))
                {
                    // Respond to the user.
                    await turnContext.SendActivityAsync("Please select a color from the suggested action choices.", cancellationToken: cancellationToken);

                    string[] path = { ".", "Cards", "SuggestedActions.json" };
                    var adaptiveCardForPersonalScope = GetFirstOptionsAdaptiveCard(path, turnContext.Activity.From.Name);

                    // Send an activity to the sender of the incoming activity.
                    await turnContext.SendActivityAsync(MessageFactory.Attachment(adaptiveCardForPersonalScope), cancellationToken);

                    // Send a suggested action card.
                    await SendSuggestedActionsAsync(turnContext, cancellationToken);
                }
                else if (text.Contains("togglevisibility"))
                {
                    string[] path = { ".", "Cards", "ToggleVisibleCard.json" };
                    var adaptiveCardForPersonalScope = GetFirstOptionsAdaptiveCard(path, turnContext.Activity.From.Name);
                    await turnContext.SendActivityAsync(MessageFactory.Attachment(adaptiveCardForPersonalScope), cancellationToken);
                }
                else if (text.Contains("red") || text.Contains("blue") || text.Contains("green"))
                {
                    var responseText = ProcessInput(text);
                    await turnContext.SendActivityAsync(responseText, cancellationToken: cancellationToken);
                    await SendSuggestedActionsAsync(turnContext, cancellationToken);
                }
                else
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text(commandString), cancellationToken);
                }
            }
            await SendDataOnCardActions(turnContext, cancellationToken);
        }

        /// <summary>
        /// Processes the input string and returns a message.
        /// </summary>
        private static string ProcessInput(string text)
        {
            const string colorText = "is the best color, I agree.";
            switch (text)
            {
                case "red":
                    return $"Red {colorText}";
                case "green":
                    return $"Green {colorText}";
                case "blue":
                    return $"Blue {colorText}";
                default:
                    return "Please select a color from the suggested action choices.";
            }
        }

        /// <summary>
        /// Creates and sends an activity with suggested actions to the user. 
        /// When the user clicks one of the buttons, the text value from the "CardAction" 
        /// will be displayed in the channel as if the user entered the text. 
        /// There are multiple "ActionTypes" that may be used for different situations.
        /// </summary>
        private static async Task SendSuggestedActionsAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var reply = MessageFactory.Text("What is your favorite color?");
            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                {
                    new CardAction() { Title = "Red", Type = ActionTypes.ImBack, Value = "Red" },
                    new CardAction() { Title = "Green", Type = ActionTypes.ImBack, Value = "Green" },
                    new CardAction() { Title = "Blue", Type = ActionTypes.ImBack, Value = "Blue" },
                },
                To = new List<string> { turnContext.Activity.From.Id },
            };

            await turnContext.SendActivityAsync(reply, cancellationToken);
        }

        /// <summary>
        /// Sends the response on card action.submit.
        /// </summary>
        private async Task SendDataOnCardActions(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.Value != null)
            {
                var reply = MessageFactory.Text("");
                reply.Text = $"Data Submitted: {turnContext.Activity.Value}";
                await turnContext.SendActivityAsync(MessageFactory.Text(reply.Text), cancellationToken);
            }
        }

        /// <summary>
        /// Gets the initial card.
        /// </summary>
        private Attachment GetFirstOptionsAdaptiveCard(string[] filepath, string name = null, string userMRI = null)
        {
            var adaptiveCardJson = File.ReadAllText(Path.Combine(filepath));
            AdaptiveCardTemplate template = new AdaptiveCardTemplate(adaptiveCardJson);
            var payloadData = new
            {
                createdById = userMRI,
                createdBy = name
            };

            // "Expand" the template - this generates the final Adaptive Card payload.
            var cardJsonstring = template.Expand(payloadData);
            var adaptiveCardAttachment = new Attachment()
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = cardJsonstring,
            };

            return adaptiveCardAttachment;
        }
    }
}
