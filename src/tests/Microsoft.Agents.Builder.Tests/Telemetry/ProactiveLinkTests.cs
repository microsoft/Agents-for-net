// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Storage;
using Microsoft.Agents.TestSupport;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.Telemetry
{
    [Collection("TelemetryTests")]
    public class ProactiveLinkTests : TelemetryScopeTestBase
    {
        private readonly Mock<IChannelAdapter> _adapter = new();
        private readonly Proactive _proactive;
        private readonly Conversation _conversation;

        public ProactiveLinkTests()
        {
            var storage = new MemoryStorage();
            var options = new AgentApplicationOptions(storage)
            {
                Proactive = new ProactiveOptions(storage)
            };
            _proactive = new Proactive(new AgentApplication(options));

            var reference = new ConversationReference
            {
                Conversation = new ConversationAccount { Id = "linked-conversation" },
                ServiceUrl = "https://test.com",
                User = new ChannelAccount("user", "User"),
                Agent = new ChannelAccount("agent", "Agent"),
                ChannelId = "test-channel"
            };
            _conversation = new Conversation(
                new Dictionary<string, string> { ["aud"] = "agent" },
                reference);
        }

        [Fact]
        public async Task StoreConversationAsync_CapturesStoreActivityContext()
        {
            using var parentActivity = StartW3CActivity();
            await _proactive.StoreConversationAsync(_conversation);

            var storeActivity = StoppedActivities.Single(
                activity => activity.OperationName == "agents.proactive.store_conversation");

            Assert.True(_conversation.ActivityContext.HasValue);
            Assert.Equal(storeActivity.TraceId, _conversation.ActivityContext.Value.TraceId);
            Assert.Equal(storeActivity.SpanId, _conversation.ActivityContext.Value.SpanId);
            Assert.Equal(storeActivity.ActivityTraceFlags, _conversation.ActivityContext.Value.TraceFlags);
        }

        [Fact]
        public async Task ConversationActivityContext_RoundTripsThroughJson()
        {
            using var parentActivity = StartW3CActivity();
            await _proactive.StoreConversationAsync(_conversation);

            var json = JsonSerializer.Serialize(
                _conversation,
                ProtocolJsonSerializer.SerializationOptions);
            var roundTripped = JsonSerializer.Deserialize<Conversation>(
                json,
                ProtocolJsonSerializer.SerializationOptions);

            Assert.NotNull(roundTripped);
            Assert.True(roundTripped.ActivityContext.HasValue);
            Assert.Equal(_conversation.ActivityContext.Value.TraceId, roundTripped.ActivityContext.Value.TraceId);
            Assert.Equal(_conversation.ActivityContext.Value.SpanId, roundTripped.ActivityContext.Value.SpanId);
            Assert.Equal(_conversation.ActivityContext.Value.TraceFlags, roundTripped.ActivityContext.Value.TraceFlags);
            Assert.Equal(_conversation.ActivityContext.Value.TraceState, roundTripped.ActivityContext.Value.TraceState);
            Assert.False(_conversation.ActivityContext.Value.IsRemote);
            Assert.True(roundTripped.ActivityContext.Value.IsRemote);
        }

        [Fact]
        public async Task SendActivityAsync_LinksToConversationContext()
        {
            using var parentActivity = StartW3CActivity();
            await _proactive.StoreConversationAsync(_conversation);
            var storeActivity = StoppedActivities.Single(
                activity => activity.OperationName == "agents.proactive.store_conversation");
            var roundTripped = RoundTripConversation(_conversation);
            _adapter
                .Setup(adapter => adapter.ContinueConversationAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<ConversationReference>(),
                    It.IsAny<AgentCallbackHandler>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await Proactive.SendActivityAsync(
                _adapter.Object,
                roundTripped,
                new Activity { Type = ActivityTypes.Message });

            var sendActivity = StoppedActivities.Single(
                activity => activity.OperationName == "agents.proactive.send_activity");
            var link = Assert.Single(sendActivity.Links);
            Assert.Equal(storeActivity.TraceId, link.Context.TraceId);
            Assert.Equal(storeActivity.SpanId, link.Context.SpanId);
            Assert.True(link.Context.IsRemote);
        }

        [Fact]
        public async Task ContinueConversationAsync_LinksToConversationContext()
        {
            using var parentActivity = StartW3CActivity();
            await _proactive.StoreConversationAsync(_conversation);
            var storeActivity = StoppedActivities.Single(
                activity => activity.OperationName == "agents.proactive.store_conversation");
            var roundTripped = RoundTripConversation(_conversation);
            _adapter
                .Setup(adapter => adapter.ProcessProactiveAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<IActivity>(),
                    It.IsAny<string>(),
                    It.IsAny<AgentCallbackHandler>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _proactive.ContinueConversationAsync(
                _adapter.Object,
                roundTripped,
                (turnContext, turnState, cancellationToken) => Task.CompletedTask);

            var continueActivity = StoppedActivities.Single(
                activity => activity.OperationName == "agents.proactive.continue_conversation");
            var link = Assert.Single(continueActivity.Links);
            Assert.Equal(storeActivity.TraceId, link.Context.TraceId);
            Assert.Equal(storeActivity.SpanId, link.Context.SpanId);
            Assert.True(link.Context.IsRemote);
        }

        private static Conversation RoundTripConversation(Conversation conversation)
        {
            var json = JsonSerializer.Serialize(
                conversation,
                ProtocolJsonSerializer.SerializationOptions);

            return JsonSerializer.Deserialize<Conversation>(
                json,
                ProtocolJsonSerializer.SerializationOptions);
        }

        private static System.Diagnostics.Activity StartW3CActivity()
        {
            return new System.Diagnostics.Activity("ProactiveLinkTests")
                .SetIdFormat(System.Diagnostics.ActivityIdFormat.W3C)
                .Start();
        }
    }
}
