// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests
{
    public class ChannelAdapterTests
    {
        [Fact]
        public void AdapterSingleUse()
        {
            var a = new SimpleAdapter();
            a.Use(new CallCountingMiddleware());
        }

        [Fact]
        public void AdapterUseChaining()
        {
            var a = new SimpleAdapter();
            a.Use(new CallCountingMiddleware()).Use(new CallCountingMiddleware());
        }

        [Fact]
        public async Task PassResourceResponsesThrough()
        {
            void ValidateResponses(IActivity[] activities)
            {
                // no need to do anything.
            }

            var a = new SimpleAdapter(ValidateResponses);
            var c = new TurnContext(a, new Activity());

            var activityId = Guid.NewGuid().ToString();
            var activity = TestMessage.Message();
            activity.Id = activityId;

            var resourceResponse = await c.SendActivityAsync(activity);
            Assert.True(resourceResponse.Id == activityId, "Incorrect response Id returned");
        }

        [Fact]
        public async Task GetLocaleFromActivity()
        {
            void ValidateResponses(IActivity[] activities)
            {
                // no need to do anything.
            }

            var a = new SimpleAdapter(ValidateResponses);
            var c = new TurnContext(a, new Activity());

            var activityId = Guid.NewGuid().ToString();
            var activity = TestMessage.Message();
            activity.Id = activityId;
            activity.Locale = "de-DE";

            Task SimpleCallback(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                Assert.Equal("de-DE", turnContext.Activity.Locale);
                return Task.CompletedTask;
            }

            await a.ProcessRequest(activity, SimpleCallback, default);
        }

        [Fact]
        public async Task ContinueConversation_DirectMsgAsync()
        {
            bool callbackInvoked = false;
            var adapter = new TestAdapter(TestAdapter.CreateConversation("ContinueConversation_DirectMsgAsync"));
            ConversationReference cr = new ConversationReference
            {
                ActivityId = "activityId",
                Agent = new ChannelAccount
                {
                    Id = "channelId",
                    Name = "testChannelAccount",
                    Role = "bot",
                },
                ChannelId = "testChannel",
                ServiceUrl = "testUrl",
                Conversation = new ConversationAccount
                {
                    ConversationType = string.Empty,
                    Id = "testConversationId",
                    IsGroup = false,
                    Name = "testConversationName",
                    Role = "user",
                },
                User = new ChannelAccount
                {
                    Id = "channelId",
                    Name = "testChannelAccount",
                    Role = "bot",
                },
            };
            Task ContinueCallback(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                callbackInvoked = true;
                return Task.CompletedTask;
            }

#pragma warning disable CS0618 // Type or member is obsolete
            await adapter.ContinueConversationAsync("MyBot", cr, ContinueCallback, default);
#pragma warning restore CS0618 // Type or member is obsolete
            Assert.True(callbackInvoked);
        }

        [Fact]
        public async Task ProcessActivityAsync_InjectsFactoryStreamingResponse_WhenRegistered()
        {
            // Arrange
            var mockSr = new Mock<IStreamingResponse>();
            mockSr.Setup(s => s.IsStreamStarted()).Returns(false);

            var factory = new Mock<IStreamingResponseFactory>();
            factory.Setup(f => f.Create(It.IsAny<ITurnContext>())).Returns(mockSr.Object);

            var services = new ServiceCollection();
            services.AddStreamingResponseFactory("test-channel", factory.Object);
            var provider = services.BuildServiceProvider();

            ITurnContext capturedContext = null;
            var adapter = new TestChannelAdapter(provider);

            var activity = new Activity { Type = ActivityTypes.Message, ChannelId = "test-channel" };
            var identity = new ClaimsIdentity();

            await adapter.ProcessActivityAsync(identity, activity,
                (ctx, ct) => { capturedContext = ctx; return Task.CompletedTask; },
                CancellationToken.None);

            // The streaming response on the turn context should be our mock
            Assert.Same(mockSr.Object, capturedContext.StreamingResponse);
        }

        [Fact]
        public async Task ProcessActivityAsync_NoFactory_UsesDefaultStreamingResponse()
        {
            var adapter = new TestChannelAdapter(services: null);
            var activity = new Activity { Type = ActivityTypes.Message, ChannelId = "unknown-channel" };
            var identity = new ClaimsIdentity();

            ITurnContext capturedContext = null;
            await adapter.ProcessActivityAsync(identity, activity,
                (ctx, ct) => { capturedContext = ctx; return Task.CompletedTask; },
                CancellationToken.None);

            // Default: should be the built-in StreamingResponse (not null)
            Assert.NotNull(capturedContext.StreamingResponse);
        }

        private class TestChannelAdapter : ChannelAdapter
        {
            public TestChannelAdapter(IServiceProvider services = null)
            {
                Services = services;
            }

            public override Task<ResourceResponse[]> SendActivitiesAsync(
                ITurnContext ctx, IActivity[] activities, CancellationToken ct)
                => Task.FromResult(activities.Select(a => new ResourceResponse(a.Id ?? "id")).ToArray());
        }
    }
}
