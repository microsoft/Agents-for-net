// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.BotBuilder.Teams;
using Microsoft.Agents.BotBuilder.Testing;
using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Storage;
using Xunit;

namespace Microsoft.Agents.BotBuilder.Tests.Teams
{
    public class TeamsSSOTokenExchangeMiddlewareTests
    {
        private const string ConnectionName = "ConnectionName";
        private const string FakeExchangeableItem = "Fake token";
        private const string ExchangeId = "exchange id";
        private const string TeamsUserId = "teams.user.id";
        private const string Token = "token";

        [Fact]
        public void Constructor_ShouldThrowOnNullStorage()
        {
            Assert.Throws<ArgumentNullException>(() => new TeamsSSOTokenExchangeMiddleware(null, ConnectionName));
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullConnectionName()
        {
            Assert.Throws<ArgumentNullException>(() => new TeamsSSOTokenExchangeMiddleware(new MemoryStorage(), null));
        }

        [Fact]
        public void Constructor_ShouldThrowOnEmptyConnectionName()
        {
            Assert.Throws<ArgumentException>(() => new TeamsSSOTokenExchangeMiddleware(new MemoryStorage(), string.Empty));
        }

        [Fact]
        public async Task ExchangedTokenAsync_ShouldReturnToken()
        {
            bool wasCalled = false;
            var adapter = new TeamsSSOAdapter(CreateConversationReference())
               .Use(new TeamsSSOTokenExchangeMiddleware(new MemoryStorage(), ConnectionName));

            adapter.AddExchangeableToken(ConnectionName, Channels.Msteams, TeamsUserId, FakeExchangeableItem, Token);

            await new TestFlow(adapter, async (context, cancellationToken) =>
            {
                // note the Middleware should not cause the Responded flag to be set
                Assert.False(context.Responded);
                wasCalled = true;
                await context.SendActivityAsync("processed", cancellationToken: cancellationToken);
                await Task.CompletedTask;
            })
                .Send("test")
                .AssertReply("processed")
                .StartTestAsync();

            Assert.True(wasCalled, "Delegate was not called");
        }

        [Fact]
        public async Task ExchangedTokenAsync_ShouldSentInvokeResponseOnSecondSend()
        {
            int calledCount = 0;
            var adapter = new TeamsSSOAdapter(CreateConversationReference())
               .Use(new TeamsSSOTokenExchangeMiddleware(new MemoryStorage(), ConnectionName));

            adapter.AddExchangeableToken(ConnectionName, Channels.Msteams, TeamsUserId, FakeExchangeableItem, Token);

            await new TestFlow(adapter, async (context, cancellationToken) =>
            {
                // note the Middleware should not cause the Responded flag to be set
                Assert.False(context.Responded);
                calledCount++;
                await context.SendActivityAsync("processed", cancellationToken: cancellationToken);
                await Task.CompletedTask;
            })
                .Send("test")
                .AssertReply("processed")
                .Send("test")
                .AssertReply((activity) =>
                {
                    // When the 2nd message goes through, it is not processed due to deduplication
                    // but an invokeResponse of 200 status with empty body is sent back
                    Assert.Equal(ActivityTypes.InvokeResponse, activity.Type);
                    var invokeResponse = (activity as Activity).Value as InvokeResponse;
                    Assert.Null(invokeResponse.Body);
                    Assert.Equal(200, invokeResponse.Status);
                })
                .StartTestAsync();

            Assert.Equal(1, calledCount);
        }

        [Fact]
        public async Task ExchangedTokenAsync_ShouldNotExchangeTokenOnPreconditionFailed()
        {
            bool wasCalled = false;
            var adapter = new TeamsSSOAdapter(CreateConversationReference())
               .Use(new TeamsSSOTokenExchangeMiddleware(new MemoryStorage(), ConnectionName));

            // since this test does not setup adapter.AddExchangeableToken, the exchange will not happen

            await new TestFlow(adapter, async (context, cancellationToken) =>
            {
                wasCalled = true;
                await Task.CompletedTask;
            })
                .Send("test")
                .AssertReply((activity) =>
                {
                    Assert.Equal(ActivityTypes.InvokeResponse, activity.Type);
                    var invokeResponse = (activity as Activity).Value as InvokeResponse;
                    var tokenExchangeRequest = invokeResponse.Body as TokenExchangeInvokeResponse;
                    Assert.Equal(ConnectionName, tokenExchangeRequest.ConnectionName);
                    Assert.Equal(ExchangeId, tokenExchangeRequest.Id);
                    Assert.Equal(412, invokeResponse.Status); //412:PreconditionFailed
                })
                .StartTestAsync();

            Assert.False(wasCalled, "Delegate was called");
        }

        [Fact]
        public async Task ExchangedTokenAsync_ShouldNotExchangeTokenOnDirectLineChannel()
        {
            var wasCalled = false;
            var adapter = new TeamsSSOAdapter(CreateConversationReference(Channels.Directline))
               .Use(new TeamsSSOTokenExchangeMiddleware(new MemoryStorage(), ConnectionName));

            await new TestFlow(adapter, async (context, cancellationToken) =>
            {
                wasCalled = true;
                await context.SendActivityAsync("processed", cancellationToken: cancellationToken);
                await Task.CompletedTask;
            })
                .Send("test")
                .AssertReply("processed")
                .StartTestAsync();

            Assert.True(wasCalled, "Delegate was not called");
        }

        private static ConversationReference CreateConversationReference(string channelId = Channels.Msteams)
        {
            return new ConversationReference
            {
                ChannelId = channelId,
                ServiceUrl = "https://test.com",
                User = new ChannelAccount(TeamsUserId, TeamsUserId),
                Bot = new ChannelAccount("bot", "Bot"),
                Conversation = new ConversationAccount(false, "convo1", "Conversation1"),
                Locale = "en-us",
            };
        }

        private class TeamsSSOAdapter(ConversationReference conversationReference) : TestAdapter(conversationReference)
        {
            public override Task SendTextToBotAsync(string userSays, BotCallbackHandler callback, CancellationToken cancellationToken)
            {
                return ProcessActivityAsync(MakeTokenExchangeActivity(), callback, cancellationToken);
            }

            public Activity MakeTokenExchangeActivity()
            {
                return new Activity
                {
                    Type = ActivityTypes.Invoke,
                    Locale = this.Locale ?? "en-us",
                    From = Conversation.User,
                    Recipient = Conversation.Bot,
                    Conversation = Conversation.Conversation,
                    ServiceUrl = Conversation.ServiceUrl,
                    Id = Guid.NewGuid().ToString(),
                    Name = SignInConstants.TokenExchangeOperationName,
                    Value = ProtocolJsonSerializer.ToJsonElements(new TokenExchangeInvokeRequest()
                    {
                        Token = FakeExchangeableItem,
                        Id = ExchangeId,
                        ConnectionName = ConnectionName
                    })
                };
            }
        }
    }
}
