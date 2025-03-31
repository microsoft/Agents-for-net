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
        private readonly IList<CardCommand> _cardCommands;

        public CardsAgent(AgentApplicationOptions options, IHttpClientFactory httpClientFactory) : base(options)
        {
            _httpClient = httpClientFactory.CreateClient();

            _cardCommands = 
                [
                    new CardCommand("static", Cards.SendStaticSearchCardAsync),
                    new CardCommand("dynamic", Cards.SendDynamicSearchCardAsync),
                    new CardCommand("hero", Cards.SendHeroCardAsync),
                    new CardCommand("thumbnail", Cards.SendThumbnailCardAsync),
                    new CardCommand("audio", Cards.SendAudioCardAsync)
                ];

            OnConversationUpdate(ConversationUpdateEvents.MembersAdded, OnWelcomeMessageAsync);

            // Listen for query from dynamic search card
            AdaptiveCards.OnSearch("nugetpackages", SearchHandlerAsync);

            // Listen for submit buttons from Adaptive Cards
            AdaptiveCards.OnActionSubmit("StaticSubmit", StaticSubmitHandlerAsync);
            AdaptiveCards.OnActionSubmit("DynamicSubmit", DynamicSubmitHandlerAsync);

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
                    await turnContext.SendActivityAsync(CommandCardActivity(), cancellationToken: cancellationToken);
                }
            }
        }

        /// <summary>
        /// Handles displaying card types.
        /// </summary>
        private async Task OnMessageHandlerAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            var cardCommand = _cardCommands.Where(c => c.Name == turnContext.Activity.Text).FirstOrDefault();
            if (cardCommand == null)
            {
                await turnContext.SendActivityAsync(CommandCardActivity(), cancellationToken);
                return;
            }

            if (cardCommand.Name.Equals("dynamic") && !turnContext.Activity.ChannelId.Equals(Channels.Msteams))
            {
                await turnContext.SendActivityAsync("Only Teams channels support `dynamic`", cancellationToken: cancellationToken);
                return;
            }

            await cardCommand.CardHandler(turnContext, turnState, cancellationToken);
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

        // Displays a HeroCard to display card types to show.
        private Activity CommandCardActivity()
        {
            var commandCard = new HeroCard
            {
                Title = "Types of cards",
                Buttons = [.. _cardCommands.Select(c => new CardAction() { Title = c.Name, Type = ActionTypes.ImBack, Value = c.Name.ToLowerInvariant() })],
            };

            return new Activity() { Type = ActivityTypes.Message, Attachments = [commandCard.ToAttachment()] };
        }

    }

    class CardCommand(string name, RouteHandler routeHandler)
    {
        public string Name { get; set; } = name;
        public RouteHandler CardHandler { get; set; } = routeHandler;
    }
}
