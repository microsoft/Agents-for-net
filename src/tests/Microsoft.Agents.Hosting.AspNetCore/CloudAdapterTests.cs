// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.Connector.Types;
using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Hosting.AspNetCore.ActivityService;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace Microsoft.Agents.Hosting.AspNetCore.Tests
{
    public class CloudAdapterTests
    {
        [Fact]
        public void Constructor_ShouldThrowWithNullActivityTaskQueue()
        {
            var factory = new Mock<IChannelServiceClientFactory>();

            Assert.Throws<ArgumentNullException>(() => new TestAdapter(factory.Object, null));
        }

        [Fact]
        public void OnTurnError_ShouldSetMiddlewares()
        {
            var record = UseRecord();

            Assert.Single(record.Adapter.MiddlewareSet as IEnumerable<Core.Interfaces.IMiddleware>);
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
            var context = CreateHttpContext(new(ActivityTypes.Message, conversation: new(id: "test")));
            var bot = new ActivityHandler();

            record.Queue.Setup(e => e.QueueBackgroundActivity(It.IsAny<ClaimsIdentity>(), It.IsAny<Activity>(), It.IsAny<Action<InvokeResponse>>()))
                .Throws(new UnauthorizedAccessException())
                .Verifiable(Times.Once);

            await record.Adapter.ProcessAsync(context.Request, context.Response, bot, CancellationToken.None);

            Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
            record.VerifyMocks();
        }

        [Fact]
        public async Task ProcessAsync_ShouldSetInvokeResponse()
        {
            var record = UseRecord();
            var context = CreateHttpContext(new(ActivityTypes.Invoke, conversation: new(id: "test")));
            var bot = new ActivityHandler();

            await record.Adapter.ProcessAsync(context.Request, context.Response, bot, CancellationToken.None);

            Assert.Equal(StatusCodes.Status511NetworkAuthenticationRequired, context.Response.StatusCode);
        }

        [Fact]
        public async Task ProcessAsync_ShouldQueueActivity()
        {
            var record = UseRecord();
            var context = CreateHttpContext(new(ActivityTypes.Message, conversation: new(id: "test")));
            var bot = new ActivityHandler();

            record.Queue.Setup(e => e.QueueBackgroundActivity(It.IsAny<ClaimsIdentity>(), It.IsAny<Activity>(), It.IsAny<Action<InvokeResponse>>()))
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
            return context;
        }

        private static Record UseRecord()
        {
            var factory = new Mock<IChannelServiceClientFactory>();
            var queue = new Mock<IActivityTaskQueue>();
            var logger = new Mock<ILogger<IBotHttpAdapter>>();
            var middleware = new Mock<Core.Interfaces.IMiddleware>();

            var adapter = new TestAdapter(factory.Object, queue.Object, logger.Object, middlewares: middleware.Object);
            return new(adapter, factory, queue, logger);
        }

        private record Record(
            TestAdapter Adapter,
            Mock<IChannelServiceClientFactory> Factory,
            Mock<IActivityTaskQueue> Queue,
            Mock<ILogger<IBotHttpAdapter>> Logger)
        {
            public void VerifyMocks()
            {
                Mock.Verify(Factory, Queue, Logger);
            }
        }

        private class TestAdapter(
                IChannelServiceClientFactory channelServiceClientFactory,
                IActivityTaskQueue activityTaskQueue,
                ILogger<IBotHttpAdapter> logger = null,
                params Core.Interfaces.IMiddleware[] middlewares)
            : CloudAdapter(channelServiceClientFactory, activityTaskQueue, logger, null, middlewares)
        {
            public override Task<InvokeResponse> ProcessActivityAsync(ClaimsIdentity claimsIdentity, IActivity activity, BotCallbackHandler callback, CancellationToken cancellationToken)
            {
                return Task.FromResult(new InvokeResponse { Status = StatusCodes.Status511NetworkAuthenticationRequired, Body = activity });
            }
        }
    }
}