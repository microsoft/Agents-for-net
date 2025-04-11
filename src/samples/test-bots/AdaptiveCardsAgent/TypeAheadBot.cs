// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AdaptiveCardsBot.Model;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.BotBuilder.App.AdaptiveCards;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace AdaptiveCardsBot
{
    /// <summary>
    /// Defines the activity handlers.
    /// </summary>
    public class TypeAheadBot : AgentApplication
    {
        private readonly string _staticSearchCardFilePath = Path.Combine(".", "Resources", "StaticSearchCard.json");
        private readonly string _dynamicSearchCardFilePath = Path.Combine(".", "Resources", "DynamicSearchCard.json");

        private readonly HttpClient _httpClient;

        public TypeAheadBot(AgentApplicationOptions options, IHttpClientFactory httpClientFactory) : base(options)
        {
            _httpClient = httpClientFactory.CreateClient("WebClient");

            OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
            OnMessage(new Regex(@"static", RegexOptions.IgnoreCase), StaticMessageHandlerAsync);
            OnMessage(new Regex(@"dynamic", RegexOptions.IgnoreCase), DynamicMessageHandlerAsync);

            // Listen for query from dynamic search card
            AdaptiveCards.OnSearch("nugetpackages", SearchHandlerAsync);
            // Listen for submit buttons
            AdaptiveCards.OnActionSubmit("StaticSubmit", StaticSubmitHandlerAsync);
            AdaptiveCards.OnActionSubmit("DynamicSubmit", DynamicSubmitHandlerAsync);

            // Listen for ANY message to be received. MUST BE AFTER ANY OTHER HANDLERS
            OnActivity(ActivityTypes.Message, MessageHandlerAsync);
        }

        /// <summary>
        /// Handles members added events.
        /// </summary>
        protected async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync("Hello and welcome! With this sample you can see the functionality of static and dynamic search in adaptive card.", cancellationToken: cancellationToken);
                    await turnContext.SendActivityAsync("Send `static` or `dynamic`", cancellationToken: cancellationToken);
                }
            }
        }

        /// <summary>
        /// Handles "static" message.
        /// </summary>
        protected async Task StaticMessageHandlerAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            Attachment attachment = CreateAdaptiveCardAttachment(_staticSearchCardFilePath);
            await turnContext.SendActivityAsync(MessageFactory.Attachment(attachment), cancellationToken);
        }

        /// <summary>
        /// Handles "dynamic" message.
        /// </summary>
        protected async Task DynamicMessageHandlerAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            Attachment attachment = CreateAdaptiveCardAttachment(_dynamicSearchCardFilePath);
            await turnContext.SendActivityAsync(MessageFactory.Attachment(attachment), cancellationToken);
        }

        /// <summary>
        /// Handles messages except "static" and "dynamic".
        /// </summary>
        protected async Task MessageHandlerAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            await turnContext.SendActivityAsync(MessageFactory.Text("Try saying `static` or `dynamic`."), cancellationToken);
        }

        /// <summary>
        /// Handles Adaptive Card dynamic search events.
        /// </summary>
        protected async Task<IList<AdaptiveCardsSearchResult>> SearchHandlerAsync(ITurnContext turnContext, ITurnState turnState, Query<AdaptiveCardsSearchParams> query, CancellationToken cancellationToken)
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
        protected async Task StaticSubmitHandlerAsync(ITurnContext turnContext, ITurnState turnState, object data, CancellationToken cancellationToken)
        {
            AdaptiveCardSubmitData submitData = ProtocolJsonSerializer.ToObject<AdaptiveCardSubmitData>(data);
            await turnContext.SendActivityAsync(MessageFactory.Text($"Statically selected option is: {submitData!.ChoiceSelect}"), cancellationToken);
        }

        /// <summary>
        /// Handles Adaptive Card Action.Submit events with verb "DynamicSubmit".
        /// </summary>
        protected async Task DynamicSubmitHandlerAsync(ITurnContext turnContext, ITurnState turnState, object data, CancellationToken cancellationToken)
        {
            AdaptiveCardSubmitData submitData = ProtocolJsonSerializer.ToObject<AdaptiveCardSubmitData>(data);
            await turnContext.SendActivityAsync(MessageFactory.Text($"Dynamically selected option is: {submitData!.ChoiceSelect}"), cancellationToken);
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

        private static Attachment CreateAdaptiveCardAttachment(string filePath)
        {
            var adaptiveCardJson = File.ReadAllText(filePath);
            var adaptiveCardAttachment = new Attachment()
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = adaptiveCardJson
            };
            return adaptiveCardAttachment;
        }
    }
}
