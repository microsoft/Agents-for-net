// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using HandlingCards.Model;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.AdaptiveCards;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace HandlingCards
{
    /// <summary>
    /// Displays the various card types available in Agents SDK.
    /// </summary>
    public class CardsAgent : AgentApplication
    {
        private readonly HttpClient _httpClient;

        public CardsAgent(AgentApplicationOptions options, IHttpClientFactory httpClientFactory) : base(options)
        {
            _httpClient = httpClientFactory.CreateClient();

            OnConversationUpdate(ConversationUpdateEvents.MembersAdded, OnWelcomeMessageAsync);

            // Listen for submit actions from Adaptive Cards.
            // Adaptive Cards that contain a submit action will call back to the Agent with the `value`
            // of a selection and the indicated Action.Submit `verb`.  The value of `verb` can be anything, but
            // a handler for the verb needs to be added using `AdaptiveCards.OnActionSubmit`.
            //
            // "items": [
            //   {
            //     "choices": [
            //     {
            //       "title": "Visual studio",
            //       "value": "visual_studio"
            //     }
            //   }
            // ],
            // "actions": [
            //   {
            //     "type": "Action.Submit",
            //     "title": "Submit",
            //      "data": {
            //        "verb": "StaticSubmit"
            //      }
            //   }
            // ]
            //
            // // See Resources/StaticSearchCard.json
            AdaptiveCards.OnActionSubmit("StaticSubmit", StaticSubmitHandlerAsync);
            AdaptiveCards.OnActionSubmit("DynamicSubmit", DynamicSubmitHandlerAsync);

            // Listen for Search query an Adaptive Cards.
            // Adaptive Cards `items` that contain the following will triggers a callback to the Agent
            // when the user types in the field.  The value of `dataset` can be a value of your your
            // your choosing, but you must add an `AdaptiveCards.OnSearch` handler for that value.
            // 
            // "choices.data": {
            //   "type": "Data.Query",
            //   "dataset": "nugetpackages"
            // }
            //
            // See Resources/DynamicSearchCard.json
            AdaptiveCards.OnSearch("nugetpackages", SearchHandlerAsync);

            // Listen for ANY message to be received. MUST BE AFTER ANY OTHER HANDLERS
            OnActivity(ActivityTypes.Message, OnMessageHandlerAsync);
        }

        /// <summary>
        /// Handles members added events.
        /// </summary>
        private async Task OnWelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync("Hello and welcome! With this sample you can see the functionality cards in an Agent.", cancellationToken: cancellationToken);
                    await Cards.SendCardCommandsAsync(turnContext, turnState, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Handles displaying card types.
        /// </summary>
        private async Task OnMessageHandlerAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            // For this sample, just let the Cards handle sending the selected card.
            await Cards.OnCardCommandsAsync(turnContext, turnState, cancellationToken);
        }

        /// <summary>
        /// Handles Adaptive Card dynamic search events.
        /// </summary>
        private async Task<IList<AdaptiveCardsSearchResult>> SearchHandlerAsync(ITurnContext turnContext, ITurnState turnState, Query<AdaptiveCardsSearchParams> query, CancellationToken cancellationToken)
        {
            string queryText = query.Parameters.QueryText;
            int count = query.Count;

            Package[] packages = await SearchPackages(queryText, count, cancellationToken);
            IList<AdaptiveCardsSearchResult> searchResults = packages.Select(package => new AdaptiveCardsSearchResult(package.Id!, $"{package.Id} - {package.Description}")).ToList();

            return searchResults;
        }

        /// <summary>
        /// Handles Adaptive Card Action.Submit events with verb "StaticSubmit".
        /// </summary>
        private async Task StaticSubmitHandlerAsync(ITurnContext turnContext, ITurnState turnState, object data, CancellationToken cancellationToken)
        {
            AdaptiveCardSubmitData submitData = ProtocolJsonSerializer.ToObject<AdaptiveCardSubmitData>(data);
            await turnContext.SendActivityAsync(MessageFactory.Text($"({nameof(CardsAgent)}) Statically selected option is: {submitData!.ChoiceSelect}"), cancellationToken);
        }

        /// <summary>
        /// Handles Adaptive Card Action.Submit events with verb "DynamicSubmit".
        /// </summary>
        private async Task DynamicSubmitHandlerAsync(ITurnContext turnContext, ITurnState turnState, object data, CancellationToken cancellationToken)
        {
            AdaptiveCardSubmitData submitData = ProtocolJsonSerializer.ToObject<AdaptiveCardSubmitData>(data);
            await turnContext.SendActivityAsync(MessageFactory.Text($"({nameof(CardsAgent)}) Dynamically selected option is: {submitData!.ChoiceSelect}"), cancellationToken);
        }

        private async Task<Package[]> SearchPackages(string text, int size, CancellationToken cancellationToken)
        {
            // Call NuGet Search API
            NameValueCollection query = HttpUtility.ParseQueryString(string.Empty);
            query["q"] = text;
            query["take"] = size.ToString();
            string queryString = query.ToString()!;
            string responseContent;
            try
            {
                responseContent = await _httpClient.GetStringAsync($"https://azuresearch-usnc.nuget.org/query?{queryString}", cancellationToken);
            }
            catch (Exception)
            {
                throw;
            }

            if (!string.IsNullOrWhiteSpace(responseContent))
            {
                var jobj = JsonObject.Parse(responseContent).AsObject();
                return jobj.ContainsKey("data")
                    ? ProtocolJsonSerializer.ToObject<Package[]>(jobj["data"])
                    : [];
            }
            else
            {
                return Array.Empty<Package>();
            }
        }
    }
}
