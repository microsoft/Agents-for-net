// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.Adapters;
using Microsoft.Agents.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Microsoft.Agents.Hosting.AspNetCore.Tests
{
    /// <summary>
    /// End-to-end tests for Tier 2 dispatch resolution (<see cref="AgentEndpointExtensions.ResolveAdapterAsync"/>),
    /// which peeks the inbound Activity's channelId, looks it up in the registry, casts the resolved
    /// <see cref="IChannelAdapter"/> to <see cref="IAgentHttpAdapter"/>, and falls back to the default adapter.
    /// </summary>
    public class Tier2ResolveAdapterTests
    {
        private sealed class DefaultHttpAdapter : IAgentHttpAdapter
        {
            public Task ProcessAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }

        // Channel adapter that ALSO serves HTTP (like CloudAdapter/A2AAdapter).
        private sealed class HttpChannelAdapter : ChannelAdapter, IAgentHttpAdapter
        {
            public override Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
                => Task.FromResult(Array.Empty<ResourceResponse>());

            public Task ProcessAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }

        // Channel adapter that does NOT serve HTTP (IChannelAdapter only).
        private sealed class NonHttpChannelAdapter : ChannelAdapter
        {
            public override Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
                => Task.FromResult(Array.Empty<ResourceResponse>());
        }

        private sealed class StubRegistry : IChannelAdapterRegistry
        {
            private readonly Dictionary<string, IChannelAdapter> _map = new(StringComparer.OrdinalIgnoreCase);

            public void Add(string channelId, IChannelAdapter adapter) => _map[channelId] = adapter;

            public bool HasChannelSpecificAdapters => _map.Count > 0;

            public IChannelAdapter GetDefault() => throw new NotSupportedException();

            public IChannelAdapter GetAdapter(string channelId) => _map[channelId];

            public bool TryGetAdapter(string channelId, out IChannelAdapter adapter)
                => _map.TryGetValue(channelId, out adapter);

            public IEnumerable<IChannelAdapter> GetAll() => _map.Values;
        }

        private static HttpRequest MakeRequest(string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            var ctx = new DefaultHttpContext();
            ctx.Request.Body = new MemoryStream(bytes);
            ctx.Request.ContentLength = bytes.Length;
            ctx.Request.ContentType = "application/json";
            return ctx.Request;
        }

        private static HttpRequest MakeChunkedRequest(string json)
        {
            // No ContentLength => exercises the chunked/unknown-length peek path.
            var bytes = Encoding.UTF8.GetBytes(json);
            var ctx = new DefaultHttpContext();
            ctx.Request.Body = new MemoryStream(bytes);
            ctx.Request.ContentType = "application/json";
            return ctx.Request;
        }

        [Fact]
        public async Task RegistryNotRegistered_ReturnsDefaultWithoutReadingBody()
        {
            var defaultAdapter = new DefaultHttpAdapter();
            using var services = new ServiceCollection().BuildServiceProvider();
            var ctx = new DefaultHttpContext();
            ctx.Request.Body = new ThrowOnReadStream();

            var resolved = await AgentEndpointExtensions.ResolveAdapterAsync(
                services,
                defaultAdapter,
                ctx.Request,
                CancellationToken.None);

            Assert.Same(defaultAdapter, resolved);
        }

        [Fact]
        public async Task MatchingChannel_WithHttpAdapter_ReturnsChannelAdapter()
        {
            var channelAdapter = new HttpChannelAdapter();
            var defaultAdapter = new DefaultHttpAdapter();
            var registry = new StubRegistry();
            registry.Add("msteams", channelAdapter);

            var request = MakeRequest("""{"type":"message","channelId":"msteams","text":"hi"}""");

            var resolved = await AgentEndpointExtensions.ResolveAdapterAsync(registry, defaultAdapter, request, CancellationToken.None);

            Assert.Same(channelAdapter, resolved);
        }

        [Fact]
        public async Task MatchingChannel_ButNotHttpAdapter_FallsBackToDefault()
        {
            // Adapter is registered for the channel but does not implement IAgentHttpAdapter.
            var channelAdapter = new NonHttpChannelAdapter();
            var defaultAdapter = new DefaultHttpAdapter();
            var registry = new StubRegistry();
            registry.Add("msteams", channelAdapter);

            var request = MakeRequest("""{"channelId":"msteams","type":"message"}""");

            var resolved = await AgentEndpointExtensions.ResolveAdapterAsync(registry, defaultAdapter, request, CancellationToken.None);

            Assert.Same(defaultAdapter, resolved);
        }

        [Fact]
        public async Task UnregisteredChannel_FallsBackToDefault()
        {
            var defaultAdapter = new DefaultHttpAdapter();
            var registry = new StubRegistry();
            registry.Add("msteams", new HttpChannelAdapter());

            var request = MakeRequest("""{"channelId":"slack","type":"message"}""");

            var resolved = await AgentEndpointExtensions.ResolveAdapterAsync(registry, defaultAdapter, request, CancellationToken.None);

            Assert.Same(defaultAdapter, resolved);
        }

        [Fact]
        public async Task MissingChannelId_FallsBackToDefault()
        {
            var defaultAdapter = new DefaultHttpAdapter();
            var registry = new StubRegistry();
            registry.Add("msteams", new HttpChannelAdapter());

            var request = MakeRequest("""{"type":"message","text":"hi"}""");

            var resolved = await AgentEndpointExtensions.ResolveAdapterAsync(registry, defaultAdapter, request, CancellationToken.None);

            Assert.Same(defaultAdapter, resolved);
        }

        [Fact]
        public async Task MalformedBody_FallsBackToDefault()
        {
            var defaultAdapter = new DefaultHttpAdapter();
            var registry = new StubRegistry();
            registry.Add("msteams", new HttpChannelAdapter());

            var request = MakeRequest("""{"channelId":""");

            var resolved = await AgentEndpointExtensions.ResolveAdapterAsync(registry, defaultAdapter, request, CancellationToken.None);

            Assert.Same(defaultAdapter, resolved);
        }

        [Fact]
        public async Task ChunkedChannel_WithHttpAdapter_ReturnsChannelAdapter()
        {
            var channelAdapter = new HttpChannelAdapter();
            var defaultAdapter = new DefaultHttpAdapter();
            var registry = new StubRegistry();
            registry.Add("directline", channelAdapter);

            var request = MakeChunkedRequest("""{"channelId":"directline","type":"message"}""");

            var resolved = await AgentEndpointExtensions.ResolveAdapterAsync(registry, defaultAdapter, request, CancellationToken.None);

            Assert.Same(channelAdapter, resolved);
        }

        [Fact]
        public async Task AfterResolution_RequestBodyIsRewindableAndIntact()
        {
            // Tier 2 must leave the body re-readable so the resolved adapter can deserialize the full Activity.
            var json = """{"type":"message","channelId":"msteams","text":"hi"}""";
            var channelAdapter = new HttpChannelAdapter();
            var defaultAdapter = new DefaultHttpAdapter();
            var registry = new StubRegistry();
            registry.Add("msteams", channelAdapter);

            var request = MakeRequest(json);

            _ = await AgentEndpointExtensions.ResolveAdapterAsync(registry, defaultAdapter, request, CancellationToken.None);

            Assert.Equal(0, request.Body.Position);
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            var roundTrip = await reader.ReadToEndAsync();
            Assert.Equal(json, roundTrip);
        }

        [Fact]
        public async Task ChannelIdAfterLargePayload_SpanningMultipleChunks_IsResolved()
        {
            // channelId sits after ~64KB of content, forcing the incremental scanner to refill its buffer.
            var filler = new string('a', 64 * 1024);
            var json = $$"""{"type":"message","text":"{{filler}}","channelId":"msteams"}""";
            var channelAdapter = new HttpChannelAdapter();
            var defaultAdapter = new DefaultHttpAdapter();
            var registry = new StubRegistry();
            registry.Add("msteams", channelAdapter);

            var request = MakeRequest(json);

            var resolved = await AgentEndpointExtensions.ResolveAdapterAsync(registry, defaultAdapter, request, CancellationToken.None);

            Assert.Same(channelAdapter, resolved);
        }

        [Fact]
        public async Task ChunkedChannelIdAfterLargePayload_IsResolved()
        {
            // Same as above but with unknown length (no Content-Length) to exercise the chunked path.
            var filler = new string('b', 64 * 1024);
            var json = $$"""{"type":"message","text":"{{filler}}","channelId":"directline"}""";
            var channelAdapter = new HttpChannelAdapter();
            var defaultAdapter = new DefaultHttpAdapter();
            var registry = new StubRegistry();
            registry.Add("directline", channelAdapter);

            var request = MakeChunkedRequest(json);

            var resolved = await AgentEndpointExtensions.ResolveAdapterAsync(registry, defaultAdapter, request, CancellationToken.None);

            Assert.Same(channelAdapter, resolved);
        }

        [Fact]
        public async Task Cancellation_IsPropagated_NotSwallowed()
        {
            // A canceled/aborted read must surface as cancellation, not fall back to the default adapter.
            var defaultAdapter = new DefaultHttpAdapter();
            var registry = new StubRegistry();
            registry.Add("msteams", new HttpChannelAdapter());

            var ctx = new DefaultHttpContext();
            ctx.Request.Body = new ThrowOnReadStream();
            ctx.Request.ContentLength = 128;
            ctx.Request.ContentType = "application/json";

            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await AgentEndpointExtensions.ResolveAdapterAsync(registry, defaultAdapter, ctx.Request, CancellationToken.None));
        }

        // Stream that throws OperationCanceledException on read to simulate an aborted request.
        private sealed class ThrowOnReadStream : Stream
        {
            public override bool CanRead => true;
            public override bool CanSeek => true; // seekable so EnableBuffering keeps it as the body
            public override bool CanWrite => false;
            public override long Length => 128;
            public override long Position { get; set; }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
                => throw new OperationCanceledException();

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => throw new OperationCanceledException();

            public override int Read(byte[] buffer, int offset, int count) => throw new OperationCanceledException();

            public override long Seek(long offset, SeekOrigin origin) => Position = 0;
            public override void Flush() { }
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
