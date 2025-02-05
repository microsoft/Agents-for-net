// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AdaptiveCards.Templating;
using TypeaheadSearch.Models;
using System.Net.Http;
using System.Text.Json;
using System.Linq;
using System;
using System.Text.Json.Nodes;
using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.BotBuilder.Teams;
using Microsoft.Agents.Core.Models;

namespace TypeaheadSearch.Bots
{
    /// <summary>
    /// Bot Activity handler class.
    /// </summary>
    public class ActivityBot : TeamsActivityHandler
    {
        /// <summary>
        /// Handle when a message is addressed to the bot.
        /// </summary>
        /// <param name="turnContext">The turn context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.Text != null)
            {
                if (turnContext.Activity.Text.ToLower().Trim() == "staticsearch")
                {
                    string[] path = { ".", "Cards", "StaticSearchCard.json" };
                    var member = await TeamsInfo.GetMemberAsync(turnContext, turnContext.Activity.From.Id, cancellationToken);
                    var initialAdaptiveCard = GetFirstOptionsAdaptiveCard(path, turnContext.Activity.From.Name, member.Id);

                    await turnContext.SendActivityAsync(MessageFactory.Attachment(initialAdaptiveCard), cancellationToken);
                }
                else if (turnContext.Activity.Text.ToLower().Trim() == "dynamicsearch")
                {
                    string[] path = { ".", "Cards", "DynamicSearchCard.json" };
                    var member = await TeamsInfo.GetMemberAsync(turnContext, turnContext.Activity.From.Id, cancellationToken);
                    var initialAdaptiveCard = GetFirstOptionsAdaptiveCard(path, turnContext.Activity.From.Name, member.Id);

                    await turnContext.SendActivityAsync(MessageFactory.Attachment(initialAdaptiveCard), cancellationToken);
                }
                else if (turnContext.Activity.Text.ToLower().Trim() == "dependantdropdown")
                {
                    string[] path = { ".", "Cards", "DependentDropdown.json" };
                    var member = await TeamsInfo.GetMemberAsync(turnContext, turnContext.Activity.From.Id, cancellationToken);
                    var initialAdaptiveCard = GetFirstOptionsAdaptiveCard(path, turnContext.Activity.From.Name, member.Id);

                    await turnContext.SendActivityAsync(MessageFactory.Attachment(initialAdaptiveCard), cancellationToken);
                }
            }
            else if (turnContext.Activity.Value != null)
            {
                var data = JsonSerializer.Deserialize<StaticSearchCard>(turnContext.Activity.Value.ToString());
                await turnContext.SendActivityAsync(MessageFactory.Text("Selected option is: " + data.choiceSelect), cancellationToken);
            }
        }

        /// <summary>
        /// Invoked when bot (like a user) are added to the conversation.
        /// </summary>
        /// <param name="membersAdded">A list of all the members added to the conversation.</param>
        /// <param name="turnContext">Context object containing information cached for a single turn of conversation with a user.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>
        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            foreach (var member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text("Hello and welcome! With this sample you can see the functionality of static, dynamic and dependent dropdown search in adaptive card."), cancellationToken);
                }
            }
        }

        /// <summary>
        ///  Invoked when an invoke activity is received from the connector.
        /// </summary>
        /// <param name="turnContext">The turn context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected override async Task<InvokeResponse> OnInvokeActivityAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
        {
            InvokeResponse adaptiveCardResponse;

            // Check if the activity is of the expected type
            if (turnContext.Activity.Name == "application/search")
            {
                // Deserialize the incoming activity value to get dropdown and search data
                var dropdownCard = JsonSerializer.Deserialize<DependantDropdownCard>(turnContext.Activity.Value.ToString());
                var searchData = JsonSerializer.Deserialize<DynamicSearchCard>(turnContext.Activity.Value.ToString());

                // Fetch package data from the external API
                var packageResult = JsonSerializer.Deserialize<JsonElement>(await new HttpClient().GetStringAsync($"https://azuresearch-usnc.nuget.org/query?q=id:{searchData.queryText}&prerelease=true", cancellationToken));

                // Check if a country was specified in the dropdown data
                if (dropdownCard.data.choiceSelect != "")
                {
                    Object searchResponseData;

                    // Define city options based on different countries
                    var usa = new[]
                    {
                        new { title = "CA", value = "CA" },
                        new { title = "FL", value = "FL" },
                        new { title = "TX", value = "TX" }
                    };

                    var france = new[]
                    {
                        new { title = "Paris", value = "Paris" },
                        new { title = "Lyon", value = "Lyon" },
                        new { title = "Nice", value = "Nice" }
                    };

                    var india = new[]
                    {
                        new { title = "Delhi", value = "Delhi" },
                        new { title = "Mumbai", value = "Mumbai" },
                        new { title = "Pune", value = "Pune" }
                    };

                    // Normalize the country name to lowercase for comparison
                    string country = dropdownCard.data.choiceSelect.ToLower();

                    if (country == "usa")
                    {
                        searchResponseData = new
                        {
                            type = "application/vnd.microsoft.search.searchResponse",
                            value = new
                            {
                                results = usa
                            }
                        };
                    }
                    else if (country == "france")
                    {
                        searchResponseData = new
                        {
                            type = "application/vnd.microsoft.search.searchResponse",
                            value = new
                            {
                                results = france
                            }
                        };
                    }
                    else
                    {
                        searchResponseData = new
                        {
                            type = "application/vnd.microsoft.search.searchResponse",
                            value = new
                            {
                                results = india
                            }
                        };
                    }

                    // Serialize the response data to JSON
                    var jsonString = JsonSerializer.Serialize(searchResponseData);
                    JsonObject jsonData = JsonSerializer.Deserialize<JsonObject>(jsonString);

                    // Create the response with a 200 status code
                    adaptiveCardResponse = new InvokeResponse()
                    {
                        Status = 200,
                        Body = jsonData
                    };
                }
                else
                {
                    // If no country is specified, process the package results
                    var packages = packageResult.GetProperty("data").EnumerateArray().Select(item => (item.GetProperty("id").ToString(), item.GetProperty("description").ToString()));
                    var packageList = packages.Select(item => new { title = item.Item1, value = item.Item1 + " - " + item.Item2 }).ToList();

                    // Build the response data for the package list
                    var searchResponseData = new
                    {
                        type = "application/vnd.microsoft.search.searchResponse",
                        value = new
                        {
                            results = packageList
                        }
                    };

                    // Serialize the response data to JSON
                    var jsonString = JsonSerializer.Serialize(searchResponseData);
                    JsonObject jsonData = JsonSerializer.Deserialize<JsonObject>(jsonString);

                    // Create the response with a 200 status code
                    adaptiveCardResponse = new InvokeResponse()
                    {
                        Status = 200,
                        Body = jsonData
                    };
                }

                // Return the adaptive card response
                return adaptiveCardResponse;
            }

            // Return null if the activity is not recognized
            return null;
        }

        // Get initial card.
        private Attachment GetFirstOptionsAdaptiveCard(string[] filepath, string name = null, string userMRI = null)
        {
            var adaptiveCardJson = File.ReadAllText(Path.Combine(filepath));
            AdaptiveCardTemplate template = new AdaptiveCardTemplate(adaptiveCardJson);
            var payloadData = new
            {
                createdById = userMRI,
                createdBy = name
            };

            //"Expand" the template - this generates the final Adaptive Card payload
            var cardJsonstring = template.Expand(payloadData);
            var adaptiveCardAttachment = new Attachment()
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = JsonSerializer.Deserialize<JsonObject>(cardJsonstring),
            };

            return adaptiveCardAttachment;
        }
    }
}
