// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Hosting.DirectLine.NamedPipes.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes.Tests
{
    /// <summary>
    /// Boundary condition tests for <see cref="NamedPipeActivityHandler"/>:
    /// unsupported verbs, unknown paths, empty bodies, non-JSON content types,
    /// deserialization failures, and attachment merging edge cases.
    /// </summary>
    public class NamedPipeActivityHandlerBoundaryTests
    {
        private readonly NamedPipeActivityHandler _handler;
        private readonly Mock<IChannelAdapter> _mockAdapter;
        private readonly Mock<IAgent> _mockAgent;

        public NamedPipeActivityHandlerBoundaryTests()
        {
            _mockAdapter = new Mock<IChannelAdapter>();
            _mockAgent = new Mock<IAgent>();

            var services = new ServiceCollection();
            services.AddScoped(_ => _mockAdapter.Object);
            services.AddScoped(_ => _mockAgent.Object);
            var sp = services.BuildServiceProvider();

            _handler = new NamedPipeActivityHandler(
                sp,
                NullLogger<NamedPipeActivityHandler>.Instance);
        }

        [Theory]
        [InlineData("GET")]
        [InlineData("PUT")]
        [InlineData("DELETE")]
        [InlineData("PATCH")]
        public async Task HandleAsync_UnsupportedVerb_Returns404(string verb)
        {
            var request = new NamedPipeRequest
            {
                Verb = verb,
                Path = "/api/messages",
                Body = Encoding.UTF8.GetBytes("{}"),
            };

            var response = await _handler.HandleAsync(request, CancellationToken.None);

            Assert.Equal(404, response.StatusCode);
        }

        [Theory]
        [InlineData("/api/other")]
        [InlineData("/v3/conversations")]
        [InlineData("/")]
        [InlineData("")]
        public async Task HandleAsync_UnknownPath_Returns404(string path)
        {
            var request = new NamedPipeRequest
            {
                Verb = "POST",
                Path = path,
                Body = Encoding.UTF8.GetBytes("{}"),
            };

            var response = await _handler.HandleAsync(request, CancellationToken.None);

            Assert.Equal(404, response.StatusCode);
        }

        [Fact]
        public async Task HandleAsync_NullBody_Returns400()
        {
            var request = new NamedPipeRequest
            {
                Verb = "POST",
                Path = "/api/messages",
                Body = null,
            };

            var response = await _handler.HandleAsync(request, CancellationToken.None);

            Assert.Equal(400, response.StatusCode);
        }

        [Fact]
        public async Task HandleAsync_EmptyBody_Returns400()
        {
            var request = new NamedPipeRequest
            {
                Verb = "POST",
                Path = "/api/messages",
                Body = [],
            };

            var response = await _handler.HandleAsync(request, CancellationToken.None);

            Assert.Equal(400, response.StatusCode);
        }

        [Theory]
        [InlineData("text/plain")]
        [InlineData("image/png")]
        [InlineData("application/xml")]
        [InlineData("multipart/form-data")]
        public async Task HandleAsync_NonJsonContentType_Returns415(string contentType)
        {
            var request = new NamedPipeRequest
            {
                Verb = "POST",
                Path = "/api/messages",
                Body = Encoding.UTF8.GetBytes("{}"),
                ContentType = contentType,
            };

            var response = await _handler.HandleAsync(request, CancellationToken.None);

            Assert.Equal(415, response.StatusCode);
        }

        [Theory]
        [InlineData("application/json")]
        [InlineData("application/json; charset=utf-8")]
        [InlineData("APPLICATION/JSON")]
        public async Task HandleAsync_JsonContentTypes_Accepted(string contentType)
        {
            var activity = new Activity { Type = ActivityTypes.Message, Text = "hi" };
            var body = JsonSerializer.SerializeToUtf8Bytes(activity, ProtocolJsonSerializer.SerializationOptions);

            _mockAdapter
                .Setup(a => a.ProcessActivityAsync(
                    It.IsAny<System.Security.Claims.ClaimsIdentity>(),
                    It.IsAny<IActivity>(),
                    It.IsAny<AgentCallbackHandler>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((InvokeResponse)null);

            var request = new NamedPipeRequest
            {
                Verb = "POST",
                Path = "/api/messages",
                Body = body,
                ContentType = contentType,
            };

            var response = await _handler.HandleAsync(request, CancellationToken.None);

            Assert.Equal(202, response.StatusCode);
        }

        [Fact]
        public async Task HandleAsync_NullOrEmptyContentType_TreatedAsJson()
        {
            var activity = new Activity { Type = ActivityTypes.Message, Text = "hi" };
            var body = JsonSerializer.SerializeToUtf8Bytes(activity, ProtocolJsonSerializer.SerializationOptions);

            _mockAdapter
                .Setup(a => a.ProcessActivityAsync(
                    It.IsAny<System.Security.Claims.ClaimsIdentity>(),
                    It.IsAny<IActivity>(),
                    It.IsAny<AgentCallbackHandler>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((InvokeResponse)null);

            var request = new NamedPipeRequest
            {
                Verb = "POST",
                Path = "/api/messages",
                Body = body,
                ContentType = null,
            };

            var response = await _handler.HandleAsync(request, CancellationToken.None);

            Assert.Equal(202, response.StatusCode);
        }

        [Fact]
        public async Task HandleAsync_InvalidJson_Returns500()
        {
            var request = new NamedPipeRequest
            {
                Verb = "POST",
                Path = "/api/messages",
                Body = Encoding.UTF8.GetBytes("{ not valid json !!!"),
                ContentType = "application/json",
            };

            var response = await _handler.HandleAsync(request, CancellationToken.None);

            Assert.Equal(500, response.StatusCode);
        }

        [Fact]
        public async Task HandleAsync_JsonNullActivity_Returns400()
        {
            var request = new NamedPipeRequest
            {
                Verb = "POST",
                Path = "/api/messages",
                Body = Encoding.UTF8.GetBytes("null"),
                ContentType = "application/json",
            };

            var response = await _handler.HandleAsync(request, CancellationToken.None);

            Assert.Equal(400, response.StatusCode);
        }

        [Fact]
        public async Task HandleAsync_AgentThrows_Returns500()
        {
            var activity = new Activity { Type = ActivityTypes.Message, Text = "hi" };
            var body = JsonSerializer.SerializeToUtf8Bytes(activity, ProtocolJsonSerializer.SerializationOptions);

            _mockAdapter
                .Setup(a => a.ProcessActivityAsync(
                    It.IsAny<System.Security.Claims.ClaimsIdentity>(),
                    It.IsAny<IActivity>(),
                    It.IsAny<AgentCallbackHandler>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Agent failed"));

            var request = new NamedPipeRequest
            {
                Verb = "POST",
                Path = "/api/messages",
                Body = body,
                ContentType = "application/json",
            };

            var response = await _handler.HandleAsync(request, CancellationToken.None);

            Assert.Equal(500, response.StatusCode);
        }

        [Fact]
        public async Task HandleAsync_AttachmentsMerged_OntoActivity()
        {
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hi",
                Attachments = [new Attachment { ContentType = "text/plain", Content = "existing" }],
            };
            var body = JsonSerializer.SerializeToUtf8Bytes(activity, ProtocolJsonSerializer.SerializationOptions);

            IActivity capturedActivity = null;
            _mockAdapter
                .Setup(a => a.ProcessActivityAsync(
                    It.IsAny<System.Security.Claims.ClaimsIdentity>(),
                    It.IsAny<IActivity>(),
                    It.IsAny<AgentCallbackHandler>(),
                    It.IsAny<CancellationToken>()))
                .Callback<System.Security.Claims.ClaimsIdentity, IActivity, AgentCallbackHandler, CancellationToken>(
                    (_, act, _, _) => capturedActivity = act)
                .ReturnsAsync((InvokeResponse)null);

            var request = new NamedPipeRequest
            {
                Verb = "POST",
                Path = "/api/messages",
                Body = body,
                ContentType = "application/json",
                Attachments =
                [
                    new NamedPipeAttachment { ContentType = "image/png", Body = [0x89, 0x50] },
                    new NamedPipeAttachment { ContentType = null, Body = [1, 2, 3] },
                ],
            };

            var response = await _handler.HandleAsync(request, CancellationToken.None);

            Assert.Equal(202, response.StatusCode);
            Assert.NotNull(capturedActivity);
            // 1 original + 2 merged = 3 total.
            Assert.Equal(3, capturedActivity.Attachments.Count);
            Assert.Equal("text/plain", capturedActivity.Attachments[0].ContentType);
            Assert.Equal("image/png", capturedActivity.Attachments[1].ContentType);
            Assert.Equal("application/octet-stream", capturedActivity.Attachments[2].ContentType);
        }

        [Fact]
        public async Task HandleAsync_AttachmentsWithNullBody_DefaultsToEmptyArray()
        {
            var activity = new Activity { Type = ActivityTypes.Message, Text = "hi" };
            var body = JsonSerializer.SerializeToUtf8Bytes(activity, ProtocolJsonSerializer.SerializationOptions);

            IActivity capturedActivity = null;
            _mockAdapter
                .Setup(a => a.ProcessActivityAsync(
                    It.IsAny<System.Security.Claims.ClaimsIdentity>(),
                    It.IsAny<IActivity>(),
                    It.IsAny<AgentCallbackHandler>(),
                    It.IsAny<CancellationToken>()))
                .Callback<System.Security.Claims.ClaimsIdentity, IActivity, AgentCallbackHandler, CancellationToken>(
                    (_, act, _, _) => capturedActivity = act)
                .ReturnsAsync((InvokeResponse)null);

            var request = new NamedPipeRequest
            {
                Verb = "POST",
                Path = "/api/messages",
                Body = body,
                ContentType = "application/json",
                Attachments = [new NamedPipeAttachment { ContentType = "image/gif", Body = null }],
            };

            var response = await _handler.HandleAsync(request, CancellationToken.None);

            Assert.Equal(202, response.StatusCode);
            Assert.NotNull(capturedActivity);
            Assert.Single(capturedActivity.Attachments);
            Assert.Equal(Array.Empty<byte>(), capturedActivity.Attachments[0].Content);
        }

        [Fact]
        public async Task HandleAsync_CaseInsensitivePath_PostApiMessages()
        {
            var activity = new Activity { Type = ActivityTypes.Message, Text = "hi" };
            var body = JsonSerializer.SerializeToUtf8Bytes(activity, ProtocolJsonSerializer.SerializationOptions);

            _mockAdapter
                .Setup(a => a.ProcessActivityAsync(
                    It.IsAny<System.Security.Claims.ClaimsIdentity>(),
                    It.IsAny<IActivity>(),
                    It.IsAny<AgentCallbackHandler>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((InvokeResponse)null);

            var request = new NamedPipeRequest
            {
                Verb = "POST",
                Path = "/API/MESSAGES",
                Body = body,
                ContentType = "application/json",
            };

            var response = await _handler.HandleAsync(request, CancellationToken.None);

            Assert.Equal(202, response.StatusCode);
        }

        [Fact]
        public async Task HandleAsync_CaseInsensitiveVerb_Post()
        {
            var activity = new Activity { Type = ActivityTypes.Message, Text = "hi" };
            var body = JsonSerializer.SerializeToUtf8Bytes(activity, ProtocolJsonSerializer.SerializationOptions);

            _mockAdapter
                .Setup(a => a.ProcessActivityAsync(
                    It.IsAny<System.Security.Claims.ClaimsIdentity>(),
                    It.IsAny<IActivity>(),
                    It.IsAny<AgentCallbackHandler>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((InvokeResponse)null);

            var request = new NamedPipeRequest
            {
                Verb = "post",
                Path = "/api/messages",
                Body = body,
                ContentType = "application/json",
            };

            var response = await _handler.HandleAsync(request, CancellationToken.None);

            Assert.Equal(202, response.StatusCode);
        }
    }
}
