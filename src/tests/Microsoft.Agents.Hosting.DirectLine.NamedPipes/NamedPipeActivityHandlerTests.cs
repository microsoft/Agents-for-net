// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Security.Claims;
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
    /// Tests for <see cref="NamedPipeActivityHandler"/>: per-stream ContentType handling, 415
    /// rejection of non-JSON primary bodies, and surfacing of attachment ContentType onto
    /// <see cref="Activity.Attachments"/>.
    /// </summary>
    public class NamedPipeActivityHandlerTests
    {
        [Fact]
        public async Task PrimaryStream_NonJson_Returns415()
        {
            var sp = new ServiceCollection().BuildServiceProvider();
            var handler = new NamedPipeActivityHandler(sp, NullLogger<NamedPipeActivityHandler>.Instance);

            var request = new NamedPipeRequest
            {
                Id = Guid.NewGuid(),
                Verb = "POST",
                Path = "/api/messages",
                Body = Encoding.UTF8.GetBytes("plain text, not json"),
                ContentType = "text/plain",
            };

            var response = await handler.HandleAsync(request, CancellationToken.None);
            Assert.Equal(415, response.StatusCode);
        }

        [Theory]
        [InlineData("application/jsonfoo")]       // near-miss prefix
        [InlineData("application/json-patch+json")]
        [InlineData("not a media type at all")]   // unparseable
        public async Task PrimaryStream_NearMissOrInvalidContentType_Returns415(string contentType)
        {
            var sp = new ServiceCollection().BuildServiceProvider();
            var handler = new NamedPipeActivityHandler(sp, NullLogger<NamedPipeActivityHandler>.Instance);

            var request = new NamedPipeRequest
            {
                Id = Guid.NewGuid(),
                Verb = "POST",
                Path = "/api/messages",
                Body = Encoding.UTF8.GetBytes("{}"),
                ContentType = contentType,
            };

            var response = await handler.HandleAsync(request, CancellationToken.None);
            Assert.Equal(415, response.StatusCode);
        }

        [Fact]
        public async Task PrimaryStream_JsonWithCharset_IsAccepted()
        {
            // application/json; charset=utf-8 must NOT trigger 415 (StartsWith match).
            var (sp, _, _) = BuildServices();
            var handler = new NamedPipeActivityHandler(sp, NullLogger<NamedPipeActivityHandler>.Instance);

            var activity = new Activity { Type = ActivityTypes.Message, Text = "hi", Id = "act-1" };
            var request = new NamedPipeRequest
            {
                Id = Guid.NewGuid(),
                Verb = "POST",
                Path = "/api/messages",
                Body = JsonSerializer.SerializeToUtf8Bytes(activity, ProtocolJsonSerializer.SerializationOptions),
                ContentType = "application/json; charset=utf-8",
            };

            var response = await handler.HandleAsync(request, CancellationToken.None);
            Assert.Equal(202, response.StatusCode);
        }

        [Fact]
        public async Task ActivityHandler_AttachmentContentType_AppearsOnActivity()
        {
            var (sp, _, capturedActivity) = BuildServices();
            var handler = new NamedPipeActivityHandler(sp, NullLogger<NamedPipeActivityHandler>.Instance);

            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "with attachments",
                Id = "act-2",
            };

            var request = new NamedPipeRequest
            {
                Id = Guid.NewGuid(),
                Verb = "POST",
                Path = "/api/messages",
                Body = JsonSerializer.SerializeToUtf8Bytes(activity, ProtocolJsonSerializer.SerializationOptions),
                ContentType = "application/json",
                Attachments =
                {
                    new NamedPipeAttachment { Id = Guid.NewGuid().ToString("D"), ContentType = "image/png", Body = [1, 2, 3] },
                    new NamedPipeAttachment { Id = Guid.NewGuid().ToString("D"), ContentType = "audio/wav", Body = [4, 5] },
                },
            };

            var response = await handler.HandleAsync(request, CancellationToken.None);
            Assert.Equal(202, response.StatusCode);

            Assert.NotNull(capturedActivity.Value);
            Assert.NotNull(capturedActivity.Value.Attachments);
            Assert.Equal(2, capturedActivity.Value.Attachments.Count);
            Assert.Equal("image/png", capturedActivity.Value.Attachments[0].ContentType);
            Assert.Equal("audio/wav", capturedActivity.Value.Attachments[1].ContentType);
            Assert.Equal(new byte[] { 1, 2, 3 }, (byte[])capturedActivity.Value.Attachments[0].Content);
            Assert.Equal(new byte[] { 4, 5 }, (byte[])capturedActivity.Value.Attachments[1].Content);
        }

        private static (IServiceProvider Sp, Mock<IChannelAdapter> Adapter, Box<Activity> CapturedActivity) BuildServices()
        {
            var captured = new Box<Activity>();

            var adapter = new Mock<IChannelAdapter>(MockBehavior.Loose);
            adapter
                .Setup(a => a.ProcessActivityAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<IActivity>(),
                    It.IsAny<AgentCallbackHandler>(),
                    It.IsAny<CancellationToken>()))
                .Returns<ClaimsIdentity, IActivity, AgentCallbackHandler, CancellationToken>((_, a, _, _) =>
                {
                    captured.Value = a as Activity;
                    return Task.FromResult<InvokeResponse>(null);
                });

            var agent = new Mock<IAgent>(MockBehavior.Loose);

            var services = new ServiceCollection();
            services.AddScoped(_ => adapter.Object);
            services.AddScoped(_ => agent.Object);
            return (services.BuildServiceProvider(), adapter, captured);
        }

        private sealed class Box<T> { public T Value { get; set; } }
    }
}
