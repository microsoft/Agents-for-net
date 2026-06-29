// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.AdaptiveCards;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class AdaptiveCardRouteAttributeTests
    {
        [Fact]
        public async Task AdaptiveCardActionExecuteRouteAttribute_MatchesVerb()
        {
            // Arrange
            var turnContext = new TurnContext(new SimpleAdapter(), new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "adaptiveCard/action",
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new
                {
                    action = new
                    {
                        type = "Action.Execute",
                        verb = "doStuff",
                        data = new { testKey = "test-value" }
                    }
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestAdaptiveCardAttributeApp(new(() => turnState.Result) { StartTypingTimer = false });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Contains(nameof(TestAdaptiveCardAttributeApp.OnActionExecuteAsync), app.Calls);
        }

        [Fact]
        public async Task AdaptiveCardActionSubmitRouteAttribute_MatchesVerb()
        {
            // Arrange
            var turnContext = new TurnContext(new SimpleAdapter(), new Activity()
            {
                Type = ActivityTypes.Message,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new
                {
                    verb = "ok",
                    testKey = "test-value"
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestAdaptiveCardAttributeApp(new(() => turnState.Result) { StartTypingTimer = false });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Contains(nameof(TestAdaptiveCardAttributeApp.OnActionSubmitAsync), app.Calls);
        }

        [Fact]
        public async Task AdaptiveCardSearchRouteAttribute_MatchesDataset()
        {
            // Arrange
            var turnContext = new TurnContext(new SimpleAdapter(), new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "application/search",
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new
                {
                    kind = "search",
                    queryText = "test-query",
                    dataset = "npm",
                    queryOptions = new { skip = 0, top = 15 }
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestAdaptiveCardAttributeApp(new(() => turnState.Result) { StartTypingTimer = false });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Contains(nameof(TestAdaptiveCardAttributeApp.OnSearchAsync), app.Calls);
        }

        [Fact]
        public async Task AdaptiveCardSearchRouteAttribute_DatasetNotMatched()
        {
            // Arrange
            var turnContext = new TurnContext(new SimpleAdapter(), new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "application/search",
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new
                {
                    kind = "search",
                    queryText = "test-query",
                    dataset = "other",
                    queryOptions = new { skip = 0, top = 15 }
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestAdaptiveCardAttributeApp(new(() => turnState.Result) { StartTypingTimer = false });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.DoesNotContain(nameof(TestAdaptiveCardAttributeApp.OnSearchAsync), app.Calls);
        }
    }

    class TestAdaptiveCardAttributeApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> Calls { get; } = [];

        [AdaptiveCardActionExecuteRoute("doStuff")]
        public Task<AdaptiveCardInvokeResponse> OnActionExecuteAsync(ITurnContext turnContext, ITurnState turnState, object data, CancellationToken cancellationToken)
        {
            Calls.Add(nameof(OnActionExecuteAsync));
            return Task.FromResult(AdaptiveCardInvokeResponseFactory.Message("ok"));
        }

        [AdaptiveCardActionSubmitRoute("ok")]
        public Task OnActionSubmitAsync(ITurnContext turnContext, ITurnState turnState, object data, CancellationToken cancellationToken)
        {
            Calls.Add(nameof(OnActionSubmitAsync));
            return Task.CompletedTask;
        }

        [AdaptiveCardSearchRoute("npm")]
        public Task<IList<AdaptiveCardsSearchResult>> OnSearchAsync(ITurnContext turnContext, ITurnState turnState, Query<AdaptiveCardsSearchParams> query, CancellationToken cancellationToken)
        {
            Calls.Add(nameof(OnSearchAsync));
            IList<AdaptiveCardsSearchResult> results = [new("title", "value")];
            return Task.FromResult(results);
        }
    }
}
