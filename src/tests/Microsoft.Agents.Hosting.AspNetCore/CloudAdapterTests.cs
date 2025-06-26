// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure;
using Azure.Core;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.Compat;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore.Tests
{
    public class CloudAdapterTests
    {
        [Fact]
        public void Constructor_ShouldThrowWithNullActivityTaskQueue()
        {
            var factory = new Mock<IChannelServiceClientFactory>();

            Assert.Throws<ArgumentNullException>(() => new CloudAdapter(factory.Object, null));
        }

        [Fact]
        public void OnTurnError_ShouldSetMiddlewares()
        {
            var record = UseRecord(middlewares: [new Mock<Builder.IMiddleware>().Object]);

            Assert.Single(record.Adapter.MiddlewareSet as IEnumerable<Builder.IMiddleware>);
        }

        [Fact]
        public async Task OnTurnError_ShouldSendExceptionActivity()
        {
            var record = UseRecord();
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
            var record = UseRecord();
            var context = new DefaultHttpContext();
            var bot = new ActivityHandler();

            await Assert.ThrowsAsync<ArgumentNullException>(() => record.Adapter.ProcessAsync(null, context.Response, bot, CancellationToken.None));
        }

        [Fact]
        public async Task ProcessAsync_ShouldThrowWithNullHttpResponse()
        {
            var record = UseRecord();
            var context = new DefaultHttpContext();
            var bot = new ActivityHandler();

            await Assert.ThrowsAsync<ArgumentNullException>(() => record.Adapter.ProcessAsync(context.Request, null, bot, CancellationToken.None));
        }

        [Fact]
        public async Task ProcessAsync_ShouldThrowWithNullBot()
        {
            var record = UseRecord();
            var context = new DefaultHttpContext();

            await Assert.ThrowsAsync<ArgumentNullException>(() => record.Adapter.ProcessAsync(context.Request, context.Response, null, CancellationToken.None));
        }

        [Fact]
        public async Task ProcessAsync_ShouldSetMethodNotAllowedStatus()
        {
            var record = UseRecord();
            var context = new DefaultHttpContext();
            var bot = new ActivityHandler();

            await record.Adapter.ProcessAsync(context.Request, context.Response, bot, CancellationToken.None);

            Assert.Equal(StatusCodes.Status405MethodNotAllowed, context.Response.StatusCode);
            record.VerifyMocks();
        }

        [Fact]
        public async Task ProcessAsync_ShouldSetBadRequestStatus()
        {
            var record = UseRecord();
            var context = CreateHttpContext(new());
            var bot = new ActivityHandler();

            await record.Adapter.ProcessAsync(context.Request, context.Response, bot, CancellationToken.None);

            Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
            record.VerifyMocks();
        }

        [Fact]
        public async Task ProcessAsync_ShouldSetUnauthorized()
        {
            var record = UseRecord();
            var context = CreateHttpContext(new(ActivityTypes.Message, conversation: new(id: Guid.NewGuid().ToString())));
            var bot = new ActivityHandler();

            record.Queue.Setup(e => e.QueueBackgroundActivity(It.IsAny<ClaimsIdentity>(), It.IsAny<Activity>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<Type>(), It.IsAny<Action<InvokeResponse>>()))
                .Throws(new UnauthorizedAccessException())
                .Verifiable(Times.Once);

            await record.Adapter.ProcessAsync(context.Request, context.Response, bot, CancellationToken.None);

            Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
            record.VerifyMocks();
        }

        [Fact]
        public async Task ProcessAsync_ShouldSetInvokeResponseNotImplemented()
        {
            var agent = new ActivityHandler();
            var record = UseRecord();
            var context = CreateHttpContext(new Activity()
            {
                Type = ActivityTypes.Invoke, 
                DeliveryMode = DeliveryModes.ExpectReplies,
                Conversation = new(id: Guid.NewGuid().ToString())
            });

            record.Queue
                .Setup(p => p.QueueBackgroundActivity(It.IsAny<ClaimsIdentity>(), It.IsAny<IActivity>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<Type>(), It.IsAny<Action<InvokeResponse>>()))
                .Callback<ClaimsIdentity, IActivity, bool, string, Type, Action<InvokeResponse>>((identity, activity, proactive, proactiveAudience, agentType, onComplete) =>
                {
                    Task.Run(async () =>
                    {
                        // Simulate what the actual Queue does
                        var response = await record.Adapter.ProcessActivityAsync(identity, activity, agent.OnTurnAsync, CancellationToken.None);
                        onComplete(response);
                    });
                });

            await record.Adapter.ProcessAsync(context.Request, context.Response, agent, CancellationToken.None);

            // this is because ActivityHandler by default will return 501 for unnamed Invokes
            Assert.Equal(StatusCodes.Status501NotImplemented, context.Response.StatusCode);
        }

        [Fact]
        public async Task ProcessAsync_ShouldSetExpectedReplies()
        {
            // Returns an ExpectedReplies with one Activity, and Body of "TokenResponse"
            var agent = new RespondingActivityHandler();

            var record = UseRecord();
            var context = CreateHttpContext(new Activity()
            {
                Type = ActivityTypes.Invoke,
                DeliveryMode = DeliveryModes.ExpectReplies,
                Conversation = new(id: Guid.NewGuid().ToString())
            });

            record.Queue
                .Setup(p => p.QueueBackgroundActivity(It.IsAny<ClaimsIdentity>(), It.IsAny<IActivity>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<Type>(), It.IsAny<Action<InvokeResponse>>()))
                .Callback<ClaimsIdentity, IActivity, bool, string, Type, Action<InvokeResponse>>((identity, activity, proactive, proactiveAudience, agentType, onComplete) =>
                {
                    Task.Run(async () =>
                    {
                        // Simulate what the actual Queue does
                        var response = await record.Adapter.ProcessActivityAsync(identity, activity, agent.OnTurnAsync, CancellationToken.None);
                        onComplete(response);
                    });
                });

            await record.Adapter.ProcessAsync(context.Request, context.Response, agent, CancellationToken.None);

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
            Assert.Equal("token", tokenResponse.Token);
        }

        [Fact]
        public async Task ProcessAsync_ShouldSetInvokeResponse()
        {
            // Arrange: Invoke with DeliveryModes.Normal, returns an InvokeResponse with Body of "TokenResponse"

            var agent = new RespondingActivityHandler();
            var record = UseRecord();
            var context = CreateHttpContext(new Activity()
            {
                Type = ActivityTypes.Invoke,
                DeliveryMode = DeliveryModes.Normal,
                Conversation = new(id: Guid.NewGuid().ToString())
            });

            record.Queue
                .Setup(p => p.QueueBackgroundActivity(It.IsAny<ClaimsIdentity>(), It.IsAny<IActivity>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<Type>(), It.IsAny<Action<InvokeResponse>>()))
                .Callback<ClaimsIdentity, IActivity, bool, string, Type, Action<InvokeResponse>>((identity, activity, proactive, proactiveAudience, agentType, onComplete) =>
                {
                    Task.Run(async () =>
                    {
                        // Simulate what the actual Queue does
                        var response = await record.Adapter.ProcessActivityAsync(identity, activity, agent.OnTurnAsync, CancellationToken.None);
                        onComplete(response);
                    });
                });

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
                .Setup(c => c.CreateConnectorClientAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<IList<string>>(), It.IsAny<bool>()))
                .Returns(Task.FromResult(mockConnectorClient.Object));

            // Test
            await record.Adapter.ProcessAsync(context.Request, context.Response, agent, CancellationToken.None);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

            // This is testing what was actually written to the HttpResponse
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var reader = new StreamReader(context.Response.Body);
            var streamText = reader.ReadToEnd();

            var tokenResponse = ProtocolJsonSerializer.ToObject<TokenResponse>(streamText);
            Assert.NotNull(tokenResponse);
            Assert.Equal("token", tokenResponse.Token);

            // RespondingActivityHandler would have sent a single Activity
            mockConnectorClient.Verify(
                c => c.Conversations.SendToConversationAsync(It.IsAny<Activity>(), It.IsAny<CancellationToken>()), 
                Times.Once());
        }

        [Fact]
        public async Task ProcessAsync_ShouldStreamResponses()
        {
            // Returns an ExpectedReplies with one Activity, and Body of "TokenResponse"
            var agent = new RespondingActivityHandler();

            var record = UseRecord();
            var context = CreateHttpContext(new Activity()
            {
                Type = ActivityTypes.Invoke,
                DeliveryMode = DeliveryModes.Stream,
                Conversation = new(id: Guid.NewGuid().ToString())
            });

            record.Queue
                .Setup(p => p.QueueBackgroundActivity(It.IsAny<ClaimsIdentity>(), It.IsAny<IActivity>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<Type>(), It.IsAny<Action<InvokeResponse>>()))
                .Callback<ClaimsIdentity, IActivity, bool, string, Type, Action<InvokeResponse>>((identity, activity, proactive, proactiveAudience, agentType, onComplete) =>
                {
                    Task.Run(async () =>
                    {
                        // Simulate what the actual Queue does
                        var response = await record.Adapter.ProcessActivityAsync(identity, activity, agent.OnTurnAsync, CancellationToken.None);
                        onComplete(response);
                    });
                });

            await record.Adapter.ProcessAsync(context.Request, context.Response, agent, CancellationToken.None);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var reader = new StreamReader(context.Response.Body);
            var streamText = reader.ReadToEnd();

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
                    var tokenResponse = ProtocolJsonSerializer.ToObject<TokenResponse>(line.Substring(6));
                    Assert.NotNull(tokenResponse);
                    Assert.Equal("token", tokenResponse.Token);
                }

                lineNumber++;
            }
        }

        [Fact]
        public async Task ProcessAsync_ShouldQueueActivity()
        {
            var record = UseRecord();
            var context = CreateHttpContext(new(ActivityTypes.Message, conversation: new(id: Guid.NewGuid().ToString())));
            var bot = new ActivityHandler();

            record.Queue.Setup(e => e.QueueBackgroundActivity(It.IsAny<ClaimsIdentity>(), It.IsAny<Activity>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<Type>(), It.IsAny<Action<InvokeResponse>>()))
                .Verifiable(Times.Once);

            await record.Adapter.ProcessAsync(context.Request, context.Response, bot, CancellationToken.None);

            Assert.Equal(StatusCodes.Status202Accepted, context.Response.StatusCode);
            record.VerifyMocks();
        }

        [Fact]
        public async Task ProcessAsync_ShouldLogMissingConversationId()
        {
            var record = UseRecord();
            var context = CreateHttpContext(new(ActivityTypes.Message));
            var bot = new ActivityHandler();

            record.Logger.Setup(e => e.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((e, _) => e.ToString().StartsWith("BadRequest: Missing Conversation.Id")),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()))
                .Verifiable(Times.Once);

            await record.Adapter.ProcessAsync(context.Request, context.Response, bot, CancellationToken.None);

            Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
            record.VerifyMocks();
        }

        [Fact]
        public async Task ProcessAsync_ShouldLogMissingActivity()
        {
            var record = UseRecord();
            var context = CreateHttpContext();
            var bot = new ActivityHandler();

            record.Logger.Setup(e => e.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((e, _) => e.ToString().StartsWith("BadRequest: Missing activity")),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()))
                .Verifiable(Times.Once);

            await record.Adapter.ProcessAsync(context.Request, context.Response, bot, CancellationToken.None);

            Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
            record.VerifyMocks();
        }

        private static DefaultHttpContext CreateHttpContext(Activity activity = null)
        {
            var content = activity == null ? "" : JsonSerializer.Serialize(activity);
            var context = new DefaultHttpContext();
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(content));
            context.Request.Method = HttpMethods.Post;
            context.Response.Body = new MemoryStream();
            return context;
        }

        private static Record UseRecord(Builder.IMiddleware[] middlewares = null)
        {
            var factory = new Mock<IChannelServiceClientFactory>();
            var logger = new Mock<ILogger<IAgentHttpAdapter>>();
            var queue = new Mock<IActivityTaskQueue>();
            var adapter = new CloudAdapter(factory.Object, queue.Object, logger.Object, middlewares: middlewares);

            return new(adapter, factory, queue, logger);
        }

        private record Record(
            CloudAdapter Adapter,
            Mock<IChannelServiceClientFactory> Factory,
            Mock<IActivityTaskQueue> Queue,
            Mock<ILogger<IAgentHttpAdapter>> Logger)
        {
            public void VerifyMocks()
            {
                Mock.Verify(Factory, Queue, Logger);
            }
        }

        private class RespondingActivityHandler : ActivityHandler
        {
            protected override async Task<InvokeResponse> OnInvokeActivityAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
            {
                await turnContext.SendActivityAsync("Test Response", cancellationToken: cancellationToken);
                return new InvokeResponse()
                {
                    Status = (int) HttpStatusCode.OK,
                    Body = new TokenResponse() {  Token = "token" }
                };
            }
        }
    }
}