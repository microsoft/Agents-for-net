// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.Compat;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System;
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
            var bot = new ActivityHandler();
            var record = UseRecord(bot);
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
            var agent = new ActivityHandler();
            var record = UseRecord(agent);

            var activity = new Activity()
            {
                Type = ActivityTypes.Invoke,
                DeliveryMode = DeliveryModes.ExpectReplies,
                Conversation = new(id: Guid.NewGuid().ToString())
            };
            var context = CreateHttpContext(activity);
                

            await record.Service.StartAsync(CancellationToken.None);
            await record.Adapter.ProcessAsync(context.Request, context.Response, agent, CancellationToken.None);
            await record.Service.StopAsync(CancellationToken.None);

            // this is because ActivityHandler by default will return 501 for unnamed Invokes
            Assert.Equal(StatusCodes.Status501NotImplemented, context.Response.StatusCode);
        }

        [Fact]
        public async Task ProcessAsync_ShouldSetExpectedReplies()
        {
            // Returns an ExpectedReplies with one Activity, and Body of "TokenResponse"
            var agent = new RespondingActivityHandler();

            var record = UseRecord(agent);

            var activity = new Activity()
            {
                Type = ActivityTypes.Invoke,
                DeliveryMode = DeliveryModes.ExpectReplies,
                Conversation = new(id: Guid.NewGuid().ToString())
            };
            var context = CreateHttpContext(activity);

            await record.Service.StartAsync(CancellationToken.None);
            await record.Adapter.ProcessAsync(context.Request, context.Response, agent, CancellationToken.None);
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
            var agent = new RespondingActivityHandler();
            var record = UseRecord(agent);

            // Making sure each request is handled separately
            var requests = new Dictionary<string, DefaultHttpContext>();
            for (int i = 1; i <= 10; i++)
            {
                var convoId = $"{Guid.NewGuid()}:{i}";
                var activity = new Activity()
                {
                    Type = ActivityTypes.Invoke,
                    DeliveryMode = DeliveryModes.Normal,
                    Conversation = new(id: convoId)
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
                .Setup(c => c.CreateConnectorClientAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<IList<string>>(), It.IsAny<bool>()))
                .Returns(Task.FromResult(mockConnectorClient.Object));


            // Test
            await record.Service.StartAsync(CancellationToken.None);

            foreach (var request in requests)
            {
                await record.Adapter.ProcessAsync(request.Value.Request, request.Value.Response, agent, CancellationToken.None);
            }

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
            mockConnectorClient.Verify(
                c => c.Conversations.SendToConversationAsync(It.IsAny<Activity>(), It.IsAny<CancellationToken>()),
                Times.Exactly(requests.Count));
        }

        [Fact]
        public async Task ProcessAsync_ShouldStreamResponses()
        {
            // Returns an ExpectedReplies with one Activity, and Body of "TokenResponse"
            var agent = new RespondingActivityHandler();

            var record = UseRecord(agent);
            var context = CreateHttpContext(new Activity()
            {
                Type = ActivityTypes.Invoke,
                DeliveryMode = DeliveryModes.Stream,
                Conversation = new(id: Guid.NewGuid().ToString())
            });

            // Test
            await record.Service.StartAsync(CancellationToken.None);
            await record.Adapter.ProcessAsync(context.Request, context.Response, agent, CancellationToken.None);
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

            Assert.NotNull (conversationId);
            Assert.Equal(6, lineNumber);
        }

        [Fact]
        public async Task ProcessAsync_ShouldLogMissingConversationId()
        {
            var record = UseRecord();
            var context = CreateHttpContext(new(ActivityTypes.Message));
            var bot = new ActivityHandler();

            record.QueueLogger.Setup(e => e.Log(
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

            record.QueueLogger.Setup(e => e.Log(
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

        private static Record UseRecord(IAgent agent = null, Builder.IMiddleware[] middlewares = null)
        {
            var factory = new Mock<IChannelServiceClientFactory>();
            var queueLogger = new Mock<ILogger<IAgentHttpAdapter>>();
            var serviceLogger = new Mock<ILogger<HostedActivityService>>();

            var sp = new Mock<IServiceProvider>();
            sp
                .Setup(s => s.GetService(It.IsAny<Type>()))
                .Returns(agent);

            var queue = new ActivityTaskQueue();
            var adapter = new CloudAdapter(factory.Object, queue, queueLogger.Object, middlewares: middlewares);
            var service = new HostedActivityService(sp.Object, new ConfigurationBuilder().Build(), adapter, queue, serviceLogger.Object);

            return new(adapter, factory, service, queue, queueLogger, serviceLogger);
        }

        private record Record(
            CloudAdapter Adapter,
            Mock<IChannelServiceClientFactory> Factory,
            HostedActivityService Service,
            IActivityTaskQueue Queue,
            Mock<ILogger<IAgentHttpAdapter>> QueueLogger,
            Mock<ILogger<HostedActivityService>> HostedServiceLogger)
        {
            public void VerifyMocks()
            {
                Mock.Verify(Factory, QueueLogger, HostedServiceLogger);
            }
        }

        private class RespondingActivityHandler : ActivityHandler
        {
            protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
            {
                await turnContext.SendActivityAsync("OnMessage Response", cancellationToken: cancellationToken);
            }

            protected override async Task<InvokeResponse> OnInvokeActivityAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
            {
                await turnContext.SendActivityAsync("Test Response", cancellationToken: cancellationToken);
                return new InvokeResponse()
                {
                    Status = (int) HttpStatusCode.OK,
                    Body = new TokenResponse() {  Token = $"token:{turnContext.Activity.Conversation.Id}" }
                };
            }
        }
    }
}