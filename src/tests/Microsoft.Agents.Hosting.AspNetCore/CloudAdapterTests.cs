// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.Compat;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Connector.Types;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Models.Activities;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore.Tests
{
    [Collection("CloudAdapter Collection")]
    public class CloudAdapterTests
    {

        [Fact]
        public void Constructor_ShouldThrowWithNullActivityTaskQueue()
        {
            var factory = new Mock<IChannelServiceClientFactory>();

            Assert.Throws<ArgumentNullException>(() => new CloudAdapter(factory.Object, null));
        }

        [Fact]
        public async Task OnTurnError_ShouldSendExceptionActivity()
        {
            var record = UseRecord(null);
            var context = new Mock<ITurnContext>();
            var exception = new ErrorResponseException("test") { Body = new ErrorResponse() };

            context.Setup(e => e.SendActivityAsync(It.IsAny<Activity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceResponse())
                .Verifiable(Times.Once);
            context.Setup(e => e.TraceActivityAsync(
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceResponse())
                .Verifiable(Times.Once);

            await record.Adapter.OnTurnError(context.Object, exception);

            Mock.Verify(context);
            record.VerifyMocks();
        }

        [Fact]
        public async Task ProcessAsync_ShouldThrowWithNullHttpRequest()
        {
            var record = UseRecord(null);
            var context = new DefaultHttpContext();
            var bot = new ActivityHandler();

            await Assert.ThrowsAsync<ArgumentNullException>(() => record.Adapter.ProcessAsync(null, context.Response, bot, CancellationToken.None));
        }

        [Fact]
        public async Task ProcessAsync_ShouldThrowWithNullHttpResponse()
        {
            var record = UseRecord(null);
            var context = new DefaultHttpContext();
            var bot = new ActivityHandler();

            await Assert.ThrowsAsync<ArgumentNullException>(() => record.Adapter.ProcessAsync(context.Request, null, bot, CancellationToken.None));
        }

        [Fact]
        public async Task ProcessAsync_ShouldThrowWithNullBot()
        {
            var record = UseRecord(null);
            var context = new DefaultHttpContext();

            await Assert.ThrowsAsync<ArgumentNullException>(() => record.Adapter.ProcessAsync(context.Request, context.Response, null, CancellationToken.None));
        }

        [Fact]
        public async Task ProcessAsync_ShouldSetMethodNotAllowedStatus()
        {
            var record = UseRecord(null);
            var context = new DefaultHttpContext();
            var bot = new ActivityHandler();

            await record.Adapter.ProcessAsync(context.Request, context.Response, bot, CancellationToken.None);

            Assert.Equal(StatusCodes.Status405MethodNotAllowed, context.Response.StatusCode);
            record.VerifyMocks();
        }

        [Fact]
        public async Task ProcessAsync_ShouldSetBadRequestStatus()
        {
            var bot = new ActivityHandler();
            var record = UseRecord((record) => bot);
            var context = CreateHttpContext(new());  // no Activity == bad request

            await record.Adapter.ProcessAsync(context.Request, context.Response, bot, CancellationToken.None);

            Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
            record.VerifyMocks();
        }

        /*
        [Fact]
        public async Task ProcessAsync_NoOnTurnErrorLog()
        {
            var bot = new RespondingActivityHandler();
            var record = UseRecord(bot);
            var context = CreateHttpContext(new(ActivityTypes.Message, serviceUrl: "http://localhost", conversation: new(id: Guid.NewGuid().ToString())));

            record.Adapter.OnTurnError = null;

            var mockConnectorClient = new Mock<IConnectorClient>();
            mockConnectorClient
                .Setup(c => c.Conversations.ReplyToActivityAsync(It.IsAny<Activity>(), It.IsAny<CancellationToken>()))
                .Throws(new Exception("ReplyToActivityAsync"));
            mockConnectorClient
                .Setup(c => c.Conversations.SendToConversationAsync(It.IsAny<Activity>(), It.IsAny<CancellationToken>()))
                .Throws(new Exception("SendToConversationAsync"));
            record.Factory
                .Setup(c => c.CreateConnectorClientAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<IList<string>>(), It.IsAny<bool>()))
                .Returns(Task.FromResult(mockConnectorClient.Object));

            record.HostedServiceLogger
                .Setup(e => e.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((e, _) => e.ToString().Contains("Error occurred executing WorkItem")),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()))
                .Verifiable(Times.Once);

            await record.Service.StartAsync(CancellationToken.None);
            await record.Adapter.ProcessAsync(context.Request, context.Response, bot, CancellationToken.None);
            await record.Service.StopAsync(CancellationToken.None);

            Assert.Equal(StatusCodes.Status202Accepted, context.Response.StatusCode);
            Mock.Verify(record.HostedServiceLogger);
        }
        */

        [Fact]
        public async Task ProcessAsync_ShouldSetInvokeResponseNotImplemented()
        {
            var record = UseRecord((record) => new ActivityHandler());

            var activity = new Activity()
            {
                ChannelId = Channels.Test,
                Type = ActivityTypes.Invoke,
                Name = "invoke",
                DeliveryMode = DeliveryModes.ExpectReplies,
                Conversation = new(id: Guid.NewGuid().ToString()),
                Recipient = new(id: "recipientId", role: RoleTypes.Agent),
                From = new(id: "fromId", role: RoleTypes.User)
            };
            var context = CreateHttpContext(activity);


            await record.Service.StartAsync(CancellationToken.None);
            await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);
            await record.Service.StopAsync(CancellationToken.None);

            // this is because ActivityHandler by default will return 501 for unnamed Invokes
            Assert.Equal(StatusCodes.Status501NotImplemented, context.Response.StatusCode);
        }

        [Fact]
        public async Task ProcessAsync_ShouldSetExpectedReplies()
        {
            // Returns an ExpectedReplies with one Activity, and Body of "TokenResponse"
            var record = UseRecord((record) => new RespondingActivityHandler());

            var activity = new Activity()
            {
                ChannelId = Channels.Test,
                Type = ActivityTypes.Invoke,
                Name = "invoke",
                DeliveryMode = DeliveryModes.ExpectReplies,
                Conversation = new(id: Guid.NewGuid().ToString()),
                Recipient = new(id: "recipientId", role: RoleTypes.Agent),
                From = new(id: "fromId", role: RoleTypes.User)
            };
            var context = CreateHttpContext(activity);

            await record.Service.StartAsync(CancellationToken.None);
            await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);
            await record.Service.StopAsync(CancellationToken.None);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var reader = new StreamReader(context.Response.Body);
            var streamText = reader.ReadToEnd();

            var expectedReplies = ProtocolJsonSerializer.ToObject<ExpectedReplies>(streamText);

            Assert.NotNull(expectedReplies);
            Assert.NotEmpty(expectedReplies.Activities);
            Assert.NotNull(expectedReplies.Body);

            var tokenResponse = ProtocolJsonSerializer.ToObject<TokenResponse>(expectedReplies.Body);
            Assert.NotNull(tokenResponse);
            Assert.Equal($"token:{activity.Conversation.Id}", tokenResponse.Token);
        }

        [Fact]
        public async Task ProcessAsync_ShouldSetInvokeResponse()
        {
            var record = UseRecord((record) => new RespondingActivityHandler());

            // Making sure each request is handled separately
            var requests = new Dictionary<string, DefaultHttpContext>();
            for (int i = 1; i <= 10; i++)
            {
                var convoId = $"{Guid.NewGuid()}:{i}";
                var activity = new Activity()
                {
                    ChannelId = Channels.Test,
                    Type = ActivityTypes.Invoke,
                    Name = "invoke",
                    DeliveryMode = DeliveryModes.Normal,
                    Conversation = new(id: convoId),
                    Recipient = new(id: "recipientId", role: RoleTypes.Agent),
                    From = new(id: "userId", role: RoleTypes.User)
                };
                var context = CreateHttpContext(activity);
                requests.Add(convoId, context);
            }

            var mockConnectorClient = new Mock<IConnectorClient>();
            mockConnectorClient.Setup(c => c.Conversations.ReplyToActivityAsync(It.IsAny<Activity>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(
                    new ResourceResponse("replyResourceId")
                ));
            mockConnectorClient.Setup(c => c.Conversations.SendToConversationAsync(It.IsAny<Activity>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(
                        new ResourceResponse("sendResourceId")
                    ));

            record.Factory
                .Setup(c => c.CreateConnectorClientAsync(It.IsAny<ITurnContext>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(mockConnectorClient.Object));


            // Test
            await record.Service.StartAsync(CancellationToken.None);

            await Task.Run(() =>
            {
                foreach (var request in requests)
                {
                    _ = record.Adapter.ProcessAsync(request.Value.Request, request.Value.Response, record.Agent, CancellationToken.None);
                }
            });

            await Task.Delay(2000); // There is a race between StopAsync and start of background processing,  To be fixed.
            await record.Service.StopAsync(CancellationToken.None);

            foreach (var request in requests)
            {
                Assert.Equal(StatusCodes.Status200OK, request.Value.Response.StatusCode);

                // This is testing what was actually written to the HttpResponse
                request.Value.Response.Body.Seek(0, SeekOrigin.Begin);
                var reader = new StreamReader(request.Value.Response.Body);
                var streamText = reader.ReadToEnd();

                var tokenResponse = ProtocolJsonSerializer.ToObject<TokenResponse>(streamText);
                Assert.NotNull(tokenResponse);
                Assert.Equal($"token:{request.Key}", tokenResponse.Token);
            }

            // RespondingActivityHandler would have sent a single Activity
            //mockConnectorClient.Verify(
            //    c => c.Conversations.SendToConversationAsync(It.IsAny<Activity>(), It.IsAny<CancellationToken>()),
            //    Times.Exactly(requests.Count));
        }

        [Fact]
        public async Task ProcessAsync_Overlapping()
        {
            var record = UseRecord((record) => new RespondingActivityHandler());

            // Making sure each request is handled separately.  10 conversations, 3 requests each
            var requests = new Dictionary<string, DefaultHttpContext>();
            for (int i = 1; i <= 10; i++)
            {
                var convoId = $"{Guid.NewGuid()}:{i}";

                for (int message = 1; message <= 3; message++)
                {
                    var activity = new Activity()
                    {
                        ChannelId = Channels.Test,
                        Type = ActivityTypes.Message,
                        Id = $"{Guid.NewGuid()}:{message}",
                        DeliveryMode = DeliveryModes.ExpectReplies,
                        Conversation = new(id: convoId),
                        Text = $"{message}",
                        Recipient = new(id: "recipientId", role: RoleTypes.Agent),
                        From = new(id: "fromId", role: RoleTypes.User)
                    };

                    var context = CreateHttpContext(activity);
                    requests.Add($"{convoId}:{activity.Id}", context);
                }
            }

            // Test
            await record.Service.StartAsync(CancellationToken.None);

            await Task.Run(() =>
            {
                foreach (var request in requests)
                {
                    _ = record.Adapter.ProcessAsync(request.Value.Request, request.Value.Response, record.Agent, CancellationToken.None);
                }
            });

            await Task.Delay(2000);
            await record.Service.StopAsync(CancellationToken.None);

            foreach (var request in requests)
            {
                request.Value.Response.Body.Seek(0, SeekOrigin.Begin);
                var reader = new StreamReader(request.Value.Response.Body);
                var streamText = reader.ReadToEnd();

                Assert.False(string.IsNullOrEmpty(streamText));
                var expectedReplies = ProtocolJsonSerializer.ToObject<ExpectedReplies>(streamText);

                Assert.NotNull(expectedReplies);

                var response = expectedReplies.Activities[0];
                Assert.Equal($"Response {request.Key}", response.Text);
            }
        }

        [Fact]
        public async Task ProcessAsync_ShouldStreamResponses()
        {
            var record = UseRecord((record) => new RespondingActivityHandler());
            var context = CreateHttpContext(new Activity()
            {
                ChannelId = Channels.Test,
                Type = ActivityTypes.Invoke,
                Name = "invoke",
                DeliveryMode = DeliveryModes.Stream,
                Conversation = new(id: Guid.NewGuid().ToString()),
                Recipient = new(id: "recipientId", role: RoleTypes.Agent),
                From = new(id: "fromId", role: RoleTypes.User)
            });

            // Test
            await record.Service.StartAsync(CancellationToken.None);

            await Task.Run(() =>
            {
                _ = record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);
            });

            await Task.Delay(2000);
            await record.Service.StopAsync(CancellationToken.None);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var reader = new StreamReader(context.Response.Body);

            string conversationId = null;
            int lineNumber = 0;
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (lineNumber == 0)
                {
                    Assert.StartsWith("event: activity", line);
                }
                else if (lineNumber == 1)
                {
                    Assert.StartsWith("data: ", line);
                    var activity = ProtocolJsonSerializer.ToObject<Activity>(line.Substring(6));
                    Assert.NotNull(activity);
                    Assert.Equal("Test Response", activity.Text);
                    conversationId = activity.Conversation.Id;
                    Assert.NotNull(conversationId);
                }
                else if (lineNumber == 2)
                {
                    Assert.Equal(0, line.Length);
                }
                else if (lineNumber == 3)
                {
                    Assert.StartsWith("event: invokeResponse", line);
                }
                else if (lineNumber == 4)
                {
                    Assert.StartsWith("data: ", line);
                    var invokeResponse = ProtocolJsonSerializer.ToObject<InvokeResponse>(line.Substring(6));
                    Assert.NotNull(invokeResponse);

                    var tokenResponse = ProtocolJsonSerializer.ToObject<TokenResponse>(invokeResponse.Body);
                    Assert.NotNull(tokenResponse);
                    Assert.Equal($"token:{conversationId}", tokenResponse.Token);
                }

                lineNumber++;
            }

            Assert.Equal(6, lineNumber);
            Assert.NotNull(conversationId);
        }

        [Fact]
        public async Task ProcessAsync_ExpectRepliesWithContinueConversation()
        {
            // Arrange
            var record = UseRecord((record) =>
            {
                var options = new TestApplicationOptions(new MemoryStorage());
                var agent = new TestApplication(options);

                // This is the scenario where a new "inner" turn is needed, using the ConversationReference of the 
                // incoming Activity.
                agent.OnActivity(ActivityTypes.Message, async (context, state, ct) =>
                {
                    await context.SendActivityAsync($"Outer: {context.Activity.Text}", cancellationToken: ct);
                    await context.Adapter.ContinueConversationAsync(
                        context.Identity,
                        context.Activity.GetConversationReference(),
                        async (innerContext, innerCt) =>
                        {
                            await innerContext.SendActivityAsync($"Inner: {context.Activity.Text}", cancellationToken: innerCt);
                        },
                        ct);
                });

                return agent;
            });

            var activity = new Activity()
            {
                Type = ActivityTypes.Message,
                DeliveryMode = DeliveryModes.ExpectReplies,
                Conversation = new(id: Guid.NewGuid().ToString()),
                Text = "user message",
                ChannelId = Channels.Test,
                From = new ChannelAccount(id: Guid.NewGuid().ToString(), role: RoleTypes.User),
                Recipient = new(id: "recipientId", role: RoleTypes.Agent),
                Id = "1"
            };
            var context = CreateHttpContext(activity);

            // Test
            await record.Service.StartAsync(CancellationToken.None);

            await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);

            await Task.Delay(2000);
            await record.Service.StopAsync(CancellationToken.None);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var reader = new StreamReader(context.Response.Body);
            var streamText = reader.ReadToEnd();

            var expectedReplies = ProtocolJsonSerializer.ToObject<ExpectedReplies>(streamText);

            Assert.NotNull(expectedReplies);
            Assert.Equal(2, expectedReplies.Activities.Count);

            Assert.Equal("Outer: user message", expectedReplies.Activities[0].Text);
            Assert.Equal(activity.Conversation.Id, expectedReplies.Activities[0].Conversation.Id);
            Assert.Equal(activity.From.Id, expectedReplies.Activities[0].Recipient.Id);
            Assert.Equal("1", expectedReplies.Activities[0].ReplyToId);

            // Inner turn has same conversation info as Incoming
            Assert.Equal("Inner: user message", expectedReplies.Activities[1].Text);
            Assert.Equal(activity.Conversation.Id, expectedReplies.Activities[1].Conversation.Id);
            Assert.Equal(activity.From.Id, expectedReplies.Activities[1].Recipient.Id);
            Assert.Equal("1", expectedReplies.Activities[1].ReplyToId);
        }

        [Fact]
        public async Task ProcessAsync_Proactive()
        {
            // Arrange
            var proactiveReference = new ConversationReference()
            {
                ServiceUrl = "https://madeup.com",
                DeliveryMode = DeliveryModes.Normal,   // DeliverMode for proactive doesn't matter here.  Not used.
                Conversation = new(id: Guid.NewGuid().ToString()),
                ActivityId = Guid.NewGuid().ToString(),
                User = new ChannelAccount(id: Guid.NewGuid().ToString(), role: RoleTypes.User),
                Agent = new(id: "recipientId", role: RoleTypes.Agent),
            };

            var record = UseRecord((record) =>
            {
                var options = new TestApplicationOptions(new MemoryStorage());
                var agent = new TestApplication(options);

                agent.OnActivity(ActivityTypes.Message, async (context, state, ct) =>
                {
                    await context.SendActivityAsync($"Outer: {context.Activity.Text}", cancellationToken: ct);
                    await context.Adapter.ContinueConversationAsync(
                        context.Identity,
                        proactiveReference,
                        async (innerContext, innerCt) =>
                        {
                            await innerContext.SendActivityAsync($"Proactive: {context.Activity.Text}", cancellationToken: innerCt);
                        },
                        ct);
                });

                return agent;
            });

            // Capture Connector ReplyToActivity.  Proactive is always via Connector
            var proactiveActivities = new List<IActivity>();
            var mockConnectorClient = new Mock<IConnectorClient>();
            mockConnectorClient.Setup(c => c.Conversations.ReplyToActivityAsync(It.IsAny<Activity>(), It.IsAny<CancellationToken>()))
                .Callback<IActivity, CancellationToken>((response, ct) => proactiveActivities.Add(response))
                .Returns(Task.FromResult(
                    new ResourceResponse("replyResourceId")
                ));

            record.Factory
                .Setup(c => c.CreateConnectorClientAsync(It.IsAny<ITurnContext>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(mockConnectorClient.Object));

            // Using ExpectReplies, but it doesn't matter.  Do this to help separate responses to make Asserts easier
            var activity = new Activity()
            {
                Type = ActivityTypes.Message,
                DeliveryMode = DeliveryModes.ExpectReplies,
                Conversation = new(id: Guid.NewGuid().ToString()),
                Text = "user message",
                ChannelId = Channels.Test,
                From = new ChannelAccount(id: Guid.NewGuid().ToString(), role: RoleTypes.User),
                Recipient = new(id: "recipientId", role: RoleTypes.Agent),
                Id = "1"
            };
            var context = CreateHttpContext(activity);

            // Test
            await record.Service.StartAsync(CancellationToken.None);

            await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);

            await record.Service.StopAsync(CancellationToken.None);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var reader = new StreamReader(context.Response.Body);
            var streamText = reader.ReadToEnd();

            var expectedReplies = ProtocolJsonSerializer.ToObject<ExpectedReplies>(streamText);

            Assert.NotNull(expectedReplies);
            Assert.Single(expectedReplies.Activities);

            // Assert initial response was via ExpectReplies
            Assert.Equal("Outer: user message", expectedReplies.Activities[0].Text);
            Assert.Equal(activity.Conversation.Id, expectedReplies.Activities[0].Conversation.Id);
            Assert.Equal(activity.From.Id, expectedReplies.Activities[0].Recipient.Id);
            Assert.Equal("1", expectedReplies.Activities[0].ReplyToId);

            // Assert the proactive response was through Connector and to correct conversation
            Assert.Single(proactiveActivities);
            Assert.Equal("Proactive: user message", proactiveActivities[0].Text);
            Assert.Equal(proactiveReference.Conversation.Id, proactiveActivities[0].Conversation.Id);
            Assert.Equal(proactiveReference.User.Id, proactiveActivities[0].Recipient.Id);
            Assert.Equal(proactiveReference.ActivityId, proactiveActivities[0].ReplyToId);
        }

        [Fact]
        public async Task ProcessAsync_CreateConversationNormalDelivery()
        {
            // Arrange
            var turnDone = new EventWaitHandle(false, EventResetMode.AutoReset);
            var memoryStorage = new MemoryStorage();
            var origConversationId = Guid.NewGuid().ToString();
            var newConversationId = Guid.NewGuid().ToString();
            var serviceUrl = "https://service.com";
            var record = UseRecord((record) =>
            {
                var options = new TestApplicationOptions(memoryStorage);
                var agent = new TestApplication(options);

                agent.OnActivity(ActivityTypes.Message, async (context, state, ct) =>
                {
                    await context.SendActivityAsync($"Original Conversation: {context.Activity.Text}", cancellationToken: ct);
                    await context.Adapter.CreateConversationAsync(
                        "appid",
                        Channels.Test,
                        serviceUrl,
                        AgentClaims.GetTokenAudience(context.Identity),
                        new ConversationParameters() { Agent = context.Activity.From },
                        async (innerContext, innerCt) =>
                        {
                            // TurnState isn't provided in the continuation lambda.  Lets test it manually.
                            var turnState = agent.Options.TurnStateFactory();
                            await turnState.LoadStateAsync(innerContext, cancellationToken: innerCt);

                            turnState.Conversation.SetValue("lastConvoMessage", context.Activity.Text);
                            await innerContext.SendActivityAsync($"New Conversation: {context.Activity.Text}", cancellationToken: innerCt);

                            await turnState.SaveStateAsync(innerContext, cancellationToken: innerCt);
                        },
                        ct);
                    turnDone.Set();
                });

                return agent;
            });

            // Capture Connector ReplyToActivity.  Proactive is always via Connector
            var responses = new List<IActivity>();
            var mockConnectorClient = new Mock<IConnectorClient>();
            mockConnectorClient.Setup(c => c.Conversations.ReplyToActivityAsync(It.IsAny<Activity>(), It.IsAny<CancellationToken>()))
                .Callback<IActivity, CancellationToken>((response, ct) => responses.Add(response))
                .Returns(Task.FromResult(
                    new ResourceResponse("replyResourceId")
                ));
            mockConnectorClient.Setup(c => c.Conversations.SendToConversationAsync(It.IsAny<Activity>(), It.IsAny<CancellationToken>()))
                .Callback<IActivity, CancellationToken>((response, ct) => responses.Add(response))
                .Returns(Task.FromResult(
                    new ResourceResponse("sendResourceId")
                ));
            mockConnectorClient
                .Setup(c => c.Conversations.CreateConversationAsync(It.IsAny<ConversationParameters>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(
                    new ConversationResourceResponse() { Id = newConversationId }
                ));

            record.Factory
                .Setup(c => c.CreateConnectorClientAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<IList<string>>(), It.IsAny<bool>()))
                .Returns(Task.FromResult(mockConnectorClient.Object));
            record.Factory
                .Setup(c => c.CreateConnectorClientAsync(It.IsAny<ITurnContext>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(mockConnectorClient.Object));

            var activity = new Activity()
            {
                Type = ActivityTypes.Message,
                DeliveryMode = DeliveryModes.Normal,
                ServiceUrl = serviceUrl,
                Conversation = new(id: origConversationId),
                Text = "user message",
                ChannelId = Channels.Test,
                From = new ChannelAccount(id: Guid.NewGuid().ToString(), role: RoleTypes.User),
                Recipient = new(id: "recipientId", role: RoleTypes.Agent),
                Id = "1"
            };
            var context = CreateHttpContext(activity);

            // Test
            await record.Service.StartAsync(CancellationToken.None);

            await Task.Run(async () =>
            {
                await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);
                Assert.Equal(StatusCodes.Status202Accepted, context.Response.StatusCode);
                Assert.Equal(0, context.Response.Body.Length);
            });

            // Wait for turn done since we don't really know this with Normal delivery.
            turnDone.WaitOne();

            Assert.Equal(2, responses.Count);

            Assert.Equal("Original Conversation: user message", responses[0].Text);
            Assert.Equal(origConversationId, responses[0].Conversation.Id);
            Assert.Equal("1", responses[0].ReplyToId);

            Assert.Equal("New Conversation: user message", responses[1].Text);
            Assert.Equal(newConversationId, responses[1].Conversation.Id);
            Assert.Null(responses[1].ReplyToId);

            // Just read directly from conversation state
            var items = await memoryStorage.ReadAsync<IDictionary<string, object>>([$"{responses[1].ChannelId}/conversations/{responses[1].Conversation.Id}"]);
            var newConvoState = items.First().Value;
            Assert.True(newConvoState.ContainsKey("lastConvoMessage"));

            await record.Service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task ProcessAsync_ContinueConversationNormalDelivery()
        {
            // Arrange
            var turnDone = new EventWaitHandle(false, EventResetMode.AutoReset);
            var memoryStorage = new MemoryStorage();
            var initialConversationId = Guid.NewGuid().ToString();
            var proactiveConversationId = Guid.NewGuid().ToString();
            var serviceUrl = "https://service.com";

            var proactiveReference = new ConversationReference()
            {
                ServiceUrl = serviceUrl,
                DeliveryMode = DeliveryModes.Normal,   // DeliverMode for proactive doesn't matter here.  Not used.
                Conversation = new(id: proactiveConversationId),
                ActivityId = Guid.NewGuid().ToString(),
                User = new ChannelAccount(id: Guid.NewGuid().ToString(), role: RoleTypes.User),
                Agent = new(id: "recipientId", role: RoleTypes.Agent),
                ChannelId = Channels.Test
            };

            var record = UseRecord((record) =>
            {
                var options = new TestApplicationOptions(memoryStorage);
                var agent = new TestApplication(options);

                agent.OnActivity(ActivityTypes.Message, async (context, state, ct) =>
                {
                    await context.SendActivityAsync($"Original Conversation: {context.Activity.Text}", cancellationToken: ct);
                    await context.Adapter.ContinueConversationAsync(
                        context.Identity,
                        proactiveReference,
                        async (innerContext, innerCt) =>
                        {
                            // TurnState isn't provided in the continuation lambda.  Lets test it manually.
                            var turnState = agent.Options.TurnStateFactory();
                            await turnState.LoadStateAsync(innerContext, cancellationToken: innerCt);

                            turnState.Conversation.SetValue("lastConvoMessage", context.Activity.Text);
                            await innerContext.SendActivityAsync($"Proactive Conversation: {context.Activity.Text}", cancellationToken: innerCt);

                            await turnState.SaveStateAsync(innerContext, cancellationToken: innerCt);
                        },
                        ct);
                    turnDone.Set();
                });

                return agent;
            });

            // Capture Connector ReplyToActivity.  Proactive is always via Connector
            var responses = new List<IActivity>();
            var mockConnectorClient = new Mock<IConnectorClient>();
            mockConnectorClient.Setup(c => c.Conversations.ReplyToActivityAsync(It.IsAny<Activity>(), It.IsAny<CancellationToken>()))
                .Callback<IActivity, CancellationToken>((response, ct) => responses.Add(response))
                .Returns(Task.FromResult(
                    new ResourceResponse("replyResourceId")
                ));
            mockConnectorClient.Setup(c => c.Conversations.SendToConversationAsync(It.IsAny<Activity>(), It.IsAny<CancellationToken>()))
                .Callback<IActivity, CancellationToken>((response, ct) => Assert.Fail())
                .Returns(Task.FromResult(
                    new ResourceResponse("sendResourceId")
                ));

            record.Factory
                .Setup(c => c.CreateConnectorClientAsync(It.IsAny<ITurnContext>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(mockConnectorClient.Object));

            var activity = new Activity()
            {
                Type = ActivityTypes.Message,
                DeliveryMode = DeliveryModes.Normal,
                ServiceUrl = serviceUrl,
                Conversation = new(id: initialConversationId),
                Text = "user message",
                ChannelId = Channels.Test,
                From = new ChannelAccount(id: Guid.NewGuid().ToString(), role: RoleTypes.User),
                Recipient = new(id: "recipientId", role: RoleTypes.Agent),
                Id = "1"
            };
            var context = CreateHttpContext(activity);

            // Test
            await record.Service.StartAsync(CancellationToken.None);

            await Task.Run(async () =>
            {
                await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);
                Assert.Equal(StatusCodes.Status202Accepted, context.Response.StatusCode);
                Assert.Equal(0, context.Response.Body.Length);
            });

            // Wait for turn done since we don't really know this with Normal delivery.
            turnDone.WaitOne();

            Assert.Equal(2, responses.Count);

            Assert.Equal("Original Conversation: user message", responses[0].Text);
            Assert.Equal(initialConversationId, responses[0].Conversation.Id);
            Assert.Equal("1", responses[0].ReplyToId);

            Assert.Equal("Proactive Conversation: user message", responses[1].Text);
            Assert.Equal(proactiveConversationId, responses[1].Conversation.Id);
            Assert.Equal(proactiveReference.ActivityId, responses[1].ReplyToId);

            // Just read directly from conversation state
            var items = await memoryStorage.ReadAsync<IDictionary<string, object>>([$"{responses[1].ChannelId}/conversations/{responses[1].Conversation.Id}"]);
            var newConvoState = items.First().Value;
            Assert.True(newConvoState.ContainsKey("lastConvoMessage"));

            await record.Service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task ProcessAsync_OAuthExpectReplies()
        {
            // Arrange
            int attempt = 0;
            var MockGraph = new Mock<IUserAuthorization>();
            MockGraph
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    if (attempt++ == 0)
                    {
                        return Task.FromResult((TokenResponse)null);
                    }
                    return Task.FromResult(new TokenResponse() { Token = "GraphToken", Expiration = DateTime.UtcNow + TimeSpan.FromMinutes(30) });
                });
            MockGraph
                .Setup(e => e.Name)
                .Returns("graph");

            var MockConnections = new Mock<IConnections>();

            // Setup AgentApplication
            var record = UseRecord((record) =>
            {
                var options = new TestApplicationOptions(new MemoryStorage())
                {
                    UserAuthorization = new UserAuthorizationOptions(MockConnections.Object, MockGraph.Object) { AutoSignIn = UserAuthorizationOptions.AutoSignInOff }
                };
                var agent = new TestApplication(options);
                agent.OnMessage("-signin", async (context, state, ct) =>
                {
                    var token = await agent.UserAuthorization.GetTurnTokenAsync(context, cancellationToken: ct);
                    Assert.Equal("GraphToken", token);
                    await context.SendActivityAsync(token, cancellationToken: ct);
                }, autoSignInHandlers: ["graph"]);

                return agent;
            });

            // Test
            await record.Service.StartAsync(CancellationToken.None);

            // start signin
            var activity = new Activity()
            {
                Type = ActivityTypes.Message,
                DeliveryMode = DeliveryModes.ExpectReplies,
                Conversation = new(id: Guid.NewGuid().ToString()),
                Text = "-signin",
                ChannelId = Channels.Test,
                From = new ChannelAccount(id: Guid.NewGuid().ToString(), role: RoleTypes.User),
                Recipient = new(id: "recipientId", role: RoleTypes.Agent),
                Id = "1"
            };
            var context = CreateHttpContext(activity);
            await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

            // get ExpectedReplies
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var reader = new StreamReader(context.Response.Body);
            var streamText = reader.ReadToEnd();
            var expectedReplies = ProtocolJsonSerializer.ToObject<ExpectedReplies>(streamText);
            Assert.NotNull(expectedReplies);
            Assert.Empty(expectedReplies.Activities);


            // send code
            activity = new Activity()
            {
                Type = ActivityTypes.Message,
                DeliveryMode = DeliveryModes.ExpectReplies,
                Conversation = new(id: activity.Conversation.Id),
                Text = "123456",
                ChannelId = Channels.Test,
                From = new ChannelAccount(id: activity.From.Id, role: RoleTypes.User),
                Recipient = new(id: "recipientId", role: RoleTypes.Agent),
                Id = "2"
            };
            context = CreateHttpContext(activity);
            await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);

            await Task.Delay(2000); // There is a race between StopAsync and start of background processing,  To be fixed.
            await record.Service.StopAsync(CancellationToken.None);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

            // get ExpectedReplies, should have received the token in a response
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            reader = new StreamReader(context.Response.Body);
            streamText = reader.ReadToEnd();
            expectedReplies = ProtocolJsonSerializer.ToObject<ExpectedReplies>(streamText);
            Assert.NotNull(expectedReplies);
            Assert.NotEmpty(expectedReplies.Activities);

            // Assert the response was to the initial message (the "-signin" message)
            Assert.Equal("GraphToken", expectedReplies.Activities[0].Text);
            Assert.Equal(activity.Conversation.Id, expectedReplies.Activities[0].Conversation.Id);
            Assert.Equal(activity.From.Id, expectedReplies.Activities[0].Recipient.Id);
            Assert.Equal("1", expectedReplies.Activities[0].ReplyToId);
        }

        [Fact]
        public async Task ProcessAsync_ShouldLogMissingConversationId()
        {
            var record = UseRecord((record) => new ActivityHandler());
            var context = CreateHttpContext(new(ActivityTypes.Message));

            /*
            record.QueueLogger.Setup(e => e.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((e, _) => e.ToString().StartsWith("BadRequest: Missing Conversation.Id")),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()))
                .Verifiable(Times.Once);
            */

            await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);

            Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
            record.VerifyMocks();
        }

        [Fact]
        public async Task ProcessAsync_ShouldLogMissingActivity()
        {
            var record = UseRecord((record) => new ActivityHandler());
            var context = CreateHttpContext();

            /*
            record.QueueLogger.Setup(e => e.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((e, _) => e.ToString().StartsWith("BadRequest: Missing activity")),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()))
                .Verifiable(Times.Once);
            */

            await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);

            Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
            record.VerifyMocks();
        }

        private static DefaultHttpContext CreateHttpContext(Activity activity = null)
        {
            var content = activity == null ? "" : ProtocolJsonSerializer.ToJson(activity);
            var context = new DefaultHttpContext();
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(content));
            context.Request.Method = HttpMethods.Post;
            context.Response.StatusCode = 0;
            context.Response.Body = new MemoryStream();
            return context;
        }

        private static Record UseRecord(Func<Record, IAgent> createAgent, Builder.IMiddleware[] middleware = null)
        {
            var factory = new Mock<IChannelServiceClientFactory>();
            var queueLogger = new Mock<ILogger<CloudAdapter>>();
            var serviceLogger = new Mock<ILogger<HostedActivityService>>();

            var sp = new Mock<IServiceProvider>();
            var queue = new ActivityTaskQueue();
            var adapter = new CloudAdapter(factory.Object, queue, queueLogger.Object, middlewares: middleware);
            var service = new HostedActivityService(sp.Object, new ConfigurationBuilder().Build(), queue, serviceLogger.Object);

            var record = new Record(null, adapter, factory, service, queue, queueLogger, serviceLogger);

            if (createAgent != null)
            {
                record.Agent = createAgent(record);
            }

            sp.Setup(s => s.GetService(It.IsAny<Type>())).Returns(record.Agent);

            return record;
        }

        private record Record(
            IAgent Agent,
            CloudAdapter Adapter,
            Mock<IChannelServiceClientFactory> Factory,
            HostedActivityService Service,
            IActivityTaskQueue Queue,
            Mock<ILogger<CloudAdapter>> QueueLogger,
            Mock<ILogger<HostedActivityService>> HostedServiceLogger)
        {
            public void VerifyMocks()
            {
                Mock.Verify(Factory, QueueLogger, HostedServiceLogger);
            }

            public IAgent Agent { get; set; } = Agent;
        }

        private class RespondingActivityHandler : ActivityHandler
        {
            private readonly Random random = new Random();

            protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
            {
                var delay = 200 + random.Next(-101, 401);
                var message = $"Response {turnContext.Activity.Conversation.Id}:{turnContext.Activity.Id}";

                await Task.Delay(delay);

                await turnContext.SendActivityAsync(message, cancellationToken: cancellationToken);
            }

            protected override async Task<InvokeResponse> OnInvokeActivityAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
            {
                await turnContext.SendActivityAsync("Test Response", cancellationToken: cancellationToken);
                return new InvokeResponse()
                {
                    Status = (int)HttpStatusCode.OK,
                    Body = new TokenResponse() { Token = $"token:{turnContext.Activity.Conversation.Id}" }
                };
            }
        }
    }
}