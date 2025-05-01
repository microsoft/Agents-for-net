﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Connector;
using Microsoft.Agents.Connector.Types;
using Microsoft.Agents.Core.Models;
using Moq;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests
{
    public class ChannelServiceAdapterBaseTests
    {
        bool _callbackInvoked = false;

        private readonly ConversationReference _reference = new ConversationReference
        {
            Conversation = new ConversationAccount(id: "conversation-id"),
            ActivityId = "activity-id"
        };

        private readonly Activity _activity = new Activity
        {
            Conversation = new ConversationAccount(),
            ServiceUrl = "MyServiceUrl"
        };

        [Fact]
        public async Task UpdateActivityAsync_ShouldReturnUpdatedResource()
        {
            //Arrange
            var expectedResponseMessage = "updateResourceId";

            var adapter = new TestChannelAdapter(new Mock<IChannelServiceClientFactory>().Object);
            var context = new TurnContext(adapter, new Activity());
            context.Services.Set<IConnectorClient>(CreateMockConnectorClient().Object);

            //Act
            var result = await context.Adapter.UpdateActivityAsync(context, new Activity(), default);

            //Assert
            Assert.Equal(expectedResponseMessage, result.Id);
        }

        [Fact]
        public async Task UpdateActivityAsync_ShouldThrowOnNullContext()
        {
            //Arrange
            var adapter = new TestChannelAdapter(new Mock<IChannelServiceClientFactory>().Object);
            var context = new TurnContext(adapter, new Activity());

            //Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await adapter.UpdateActivityAsync(null, new Activity(), CancellationToken.None));
        }

        [Fact]
        public async Task UpdateActivityAsync_ShouldThrowOnNullActivity()
        {
            //Arrange
            var adapter = new TestChannelAdapter(new Mock<IChannelServiceClientFactory>().Object);
            var context = new TurnContext(adapter, new Activity());

            //Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await adapter.UpdateActivityAsync(context, null, CancellationToken.None));
        }

        [Fact]
        public async Task DeleteActivityAsync_ShouldCompleteTask()
        {
            //Arrange
            var connectorClient = CreateMockConnectorClient();
            var adapter = new TestChannelAdapter(new Mock<IChannelServiceClientFactory>().Object);
            var context = new TurnContext(adapter, new Activity());
            context.Services.Set<IConnectorClient>(connectorClient.Object);

            //Act
            await context.Adapter.DeleteActivityAsync(context, _reference, default);

            //Assert
            connectorClient.Verify(
                c => c.Conversations.DeleteActivityAsync("conversation-id", "activity-id", It.IsAny<CancellationToken>()),
                Times.Once
            );
        }

        [Fact]
        public async Task DeleteActivityAsync_ShouldThrowOnNullContext()
        {
            //Arrange
            var adapter = new TestChannelAdapter(new Mock<IChannelServiceClientFactory>().Object);
            var context = new TurnContext(adapter, new Activity());

            //Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await context.Adapter.DeleteActivityAsync(null, _reference, CancellationToken.None));
        }

        [Fact]
        public async Task DeleteActivityAsync_ShouldThrowOnNullReference()
        {
            //Arrange
            var adapter = new TestChannelAdapter(new Mock<IChannelServiceClientFactory>().Object);
            var context = new TurnContext(adapter, new Activity());

            //Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await context.Adapter.DeleteActivityAsync(context, null, CancellationToken.None));
        }

        [Fact]
        public async Task ContinueConversationAsync_ShouldSendMessageWithBotId()
        {
            // Arrange
            _callbackInvoked = false;
            var adapter = new TestChannelAdapter(CreateMockChannelServiceClientFactory().Object);
                  
            //Act
            await adapter.ContinueConversationAsync("MyBot", _reference, ContinueCallback, default);
            
            //Assert
            Assert.True(_callbackInvoked);
        }

        [Fact]
        public async Task ContinueConversationAsync_ShouldSendMessageWithClaims()
        {
            // Arrange
            _callbackInvoked = false;
            var adapter = new TestChannelAdapter(CreateMockChannelServiceClientFactory().Object);

            var claimsIdentity = new ClaimsIdentity(new List<Claim>
            {
                new Claim("aud", "MyBot"),
                new Claim("appId", "MyBot")
            });

            //Act
            await adapter.ContinueConversationAsync(claimsIdentity, _reference, ContinueCallback, default);

            //Assert
            Assert.True(_callbackInvoked);
        }

        [Fact]
        public async Task ContinueConversationAsync_ShouldSendMessageWithAudience()
        {
            // Arrange
            _callbackInvoked = false;
            var adapter = new TestChannelAdapter(CreateMockChannelServiceClientFactory().Object);

            var claimsIdentity = new ClaimsIdentity(new List<Claim>
            {
                new Claim("aud", "MyBot"),
                new Claim("appId", "MyBot")
            });

            //Act
            await adapter.ContinueConversationAsync(claimsIdentity, _reference, "MyAudience", ContinueCallback, default);

            //Assert
            Assert.True(_callbackInvoked);
        }

        [Fact]
        public async Task ContinueConversationAsync_ShouldSendMessageWithBotIdAndActivity()
        {
            // Arrange
            _callbackInvoked = false;
            var adapter = new TestChannelAdapter(CreateMockChannelServiceClientFactory().Object);

            //Act
            await adapter.ContinueConversationAsync("MyBot", _activity, ContinueCallback, default);

            //Assert
            Assert.True(_callbackInvoked);
        }

        [Fact]
        public async Task ContinueConversationAsync_ShouldSendMessageWithClaimsAndActivity()
        {
            // Arrange
            _callbackInvoked = false;
            var adapter = new TestChannelAdapter(CreateMockChannelServiceClientFactory().Object);

            var claimsIdentity = new ClaimsIdentity(new List<Claim>
            {
                new Claim("aud", "MyBot"),
                new Claim("appId", "MyBot")
            });

            //Act
            await adapter.ContinueConversationAsync(claimsIdentity, _activity, ContinueCallback, default);

            //Assert
            Assert.True(_callbackInvoked);
        }

        [Fact]
        public async Task ContinueConversationAsync_ShouldSendMessageWithClaimsAudienceAndActivity()
        {
            // Arrange
            _callbackInvoked = false;
            var adapter = new TestChannelAdapter(CreateMockChannelServiceClientFactory().Object);

            var claimsIdentity = new ClaimsIdentity(new List<Claim>
            {
                new Claim("aud", "MyBot"),
                new Claim("appId", "MyBot")
            });

            //Act
            await adapter.ContinueConversationAsync(claimsIdentity, _activity, "MyAudience", ContinueCallback, default);

            //Assert
            Assert.True(_callbackInvoked);
        }

        [Fact]
        public async Task CreateConversationAsync_ShouldCreateConversation()
        {
            // Arrange
            _callbackInvoked = false;
            var adapter = new TestChannelAdapter(CreateMockChannelServiceClientFactory().Object);

            var claimsIdentity = new ClaimsIdentity(new List<Claim>
            {
                new Claim("aud", "MyBot"),
                new Claim("appId", "MyBot")
            });

            //Act
            await adapter.CreateConversationAsync("MyBot", "MyChannel", "MyServiceUrl", "MyAudience", new ConversationParameters(), ContinueCallback, default);

            //Assert
            Assert.True(_callbackInvoked);
        }

        [Fact]
        public async Task SendActivitiesAsync_ShouldDelayTasks()
        {
            // Arrange
            var connectorClient = CreateMockConnectorClient();
            var adapter = new TestChannelAdapter(new Mock<IChannelServiceClientFactory>().Object);
            var context = new TurnContext(adapter, new Activity());
            context.Services.Set<IConnectorClient>(connectorClient.Object);
            var activities = new Activity[]
            {
                new Activity(type: ActivityTypes.Delay, value: 2000)
            };

            var stopwatch = new System.Diagnostics.Stopwatch();

            //Total execution time
            var expectedTotalDelay = 2000;
            var marginOfError = 200; // Allow a small margin for system overhead

            // Act
            stopwatch.Start();
            await adapter.SendActivitiesAsync(context, activities, CancellationToken.None);
            stopwatch.Stop();
            var elapsedMilliseconds = stopwatch.ElapsedMilliseconds;

            // Assert
            Assert.InRange(elapsedMilliseconds, expectedTotalDelay - marginOfError, expectedTotalDelay + marginOfError);
        }

        [Fact]
        public async Task SendActivitiesAsync_ShouldAddInvokeResponse()
        {
            // Arrange
            var adapter = new TestChannelAdapter(new Mock<IChannelServiceClientFactory>().Object);
            var context = new TurnContext(adapter, new Activity());
            var activities = new Activity[]
            {
                new Activity(type: ActivityTypes.InvokeResponse, value: "invoke response")
            };

            //Act
            await adapter.SendActivitiesAsync(context, activities, CancellationToken.None);

            //Assert
            var invokeResponse = context.StackState.Keys;
            Assert.Contains(ChannelAdapter.InvokeResponseKey, context.StackState.Keys);
        }

        [Fact]
        public async Task SendActivitiesAsync_ShouldReplyActivity()
        {
            // Arrange
            var expectedResponseMessage = "replyResourceId";

            var connectorClient = CreateMockConnectorClient();
            var adapter = new TestChannelAdapter(new Mock<IChannelServiceClientFactory>().Object);
            var context = new TurnContext(adapter, new Activity());
            context.Services.Set<IConnectorClient>(connectorClient.Object);
            var activities = new Activity[]
            {
                new Activity(type: ActivityTypes.Message, value: "reply activity", replyToId: "replyToId")
            };

            //Act
            var responses = await adapter.SendActivitiesAsync(context, activities, CancellationToken.None);

            //Assert
            Assert.Equal(expectedResponseMessage, responses[0].Id);
        }

        [Fact]
        public async Task SendActivitiesAsync_ShouldSendActivity()
        {
            // Arrange
            var expectedResponseMessage = "sendResourceId";

            var connectorClient = CreateMockConnectorClient();
            var adapter = new TestChannelAdapter(new Mock<IChannelServiceClientFactory>().Object);
            var context = new TurnContext(adapter, new Activity());
            context.Services.Set<IConnectorClient>(connectorClient.Object);
            var activities = new Activity[]
            {
                new Activity(type: ActivityTypes.Message, value: "message activity")
            };

            //Act
            var responses = await adapter.SendActivitiesAsync(context, activities, CancellationToken.None);

            //Assert
            Assert.Equal(expectedResponseMessage, responses[0].Id);
        }

        [Fact]
        public async Task SendActivitiesAsync_ShouldThrowOnActivitiesListEmpty()
        {
            // Arrange
            var adapter = new TestChannelAdapter(new Mock<IChannelServiceClientFactory>().Object);
            var context = new TurnContext(adapter, new Activity());

            //Assert
            await Assert.ThrowsAsync<ArgumentException>(async () => await adapter.SendActivitiesAsync(context, [], default));
        }

        private Task ContinueCallback(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            _callbackInvoked = true;
            return Task.CompletedTask;
        }

        private Mock<IConnectorClient> CreateMockConnectorClient()
        {
            // Arrange the Adapter.
            var mockConnectorClient = new Mock<IConnectorClient>();

            // Mock the adapter UpdateActivityAsync method
            mockConnectorClient.Setup(c => c.Conversations.UpdateActivityAsync(It.IsAny<Activity>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(
                        // Return a well known resourceId so we can assert we capture the right return value.
                        new ResourceResponse("updateResourceId")
                    ));

            // Mock the adapter DeleteActivityAsync method
            mockConnectorClient.Setup(c => c.Conversations.DeleteActivityAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask) // Simulate the deletion without actual behavior
                .Verifiable(); // Ensure the method is called during the test      

            // Mock the adapter UpdateActivityAsync method
            mockConnectorClient.Setup(c => c.Conversations.CreateConversationAsync(It.IsAny<ConversationParameters>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(
                        // Return a well known conversation resource so we can assert we capture the right return value.
                        new ConversationResourceResponse("activityId", "serviceUrl", "resourceId")
                    ));

            // Mock the adapter UpdateActivityAsync method
            mockConnectorClient.Setup(c => c.Conversations.ReplyToActivityAsync(It.IsAny<Activity>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(
                        // Return a well known resourceId so we can assert we capture the right return value.
                        new ResourceResponse("replyResourceId")
                    ));

            // Mock the adapter UpdateActivityAsync method
            mockConnectorClient.Setup(c => c.Conversations.SendToConversationAsync(It.IsAny<Activity>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(
                        // Return a well known resourceId so we can assert we capture the right return value.
                        new ResourceResponse("sendResourceId")
                    ));

            return mockConnectorClient;
        }

        private Mock<IChannelServiceClientFactory> CreateMockChannelServiceClientFactory()
        {
            var userId = "user-id";
            var connectionName = "connection-name";
            var channelId = "channel-id";
            string magicCode = null;

            var mockUserTokenClient = new Mock<IUserTokenClient>();
            mockUserTokenClient.Setup(
                x => x.GetUserTokenAsync(It.Is<string>(s => s == userId), It.Is<string>(s => s == connectionName), It.Is<string>(s => s == channelId), It.Is<string>(s => s == magicCode), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenResponse { ChannelId = channelId, ConnectionName = connectionName, Token = $"TOKEN" });

            var mockChannelServiceClientFactory = new Mock<IChannelServiceClientFactory>();
            mockChannelServiceClientFactory.Setup(
                x => x.CreateConnectorClientAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<IList<string>>(), It.IsAny<bool>()))
                .ReturnsAsync(CreateMockConnectorClient().Object);
            mockChannelServiceClientFactory.Setup(
                x => x.CreateUserTokenClientAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(mockUserTokenClient.Object);

            return mockChannelServiceClientFactory;
        }

        private class TestChannelAdapter : ChannelServiceAdapterBase
        {
            public TestChannelAdapter(IChannelServiceClientFactory channelServiceClientFactory)
                : base(channelServiceClientFactory)
            {
            }
        }
    } 
}
