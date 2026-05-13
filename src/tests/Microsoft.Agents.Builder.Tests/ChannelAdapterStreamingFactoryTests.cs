// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests
{
    public class ChannelAdapterStreamingFactoryTests
    {
        [Fact]
        public void ResolveStreamingResponseFactory_NullChannelId_ReturnsNull()
        {
            var services = new ServiceCollection().BuildServiceProvider();
            var adapter = new TestableChannelAdapter(services);

            var result = adapter.TestResolveFactory(null);

            Assert.Null(result);
        }

        [Fact]
        public void ResolveStreamingResponseFactory_NullServices_ReturnsNull()
        {
            var adapter = new TestableChannelAdapter(null);

            var result = adapter.TestResolveFactory("test-channel");

            Assert.Null(result);
        }

        [Fact]
        public void ResolveStreamingResponseFactory_WithRegistryFactory_ReturnsIt()
        {
            var services = new ServiceCollection();
            var registry = new StreamingResponseFactoryRegistry();
            var mockFactory = new Mock<IStreamingResponseFactory>();
            registry.Register("test-channel", mockFactory.Object);
            services.AddSingleton(registry);

            var provider = services.BuildServiceProvider();
            var adapter = new TestableChannelAdapter(provider);

            var result = adapter.TestResolveFactory("test-channel");

            Assert.Same(mockFactory.Object, result);
        }

        [Fact]
        public void ResolveStreamingResponseFactory_NoRegistryNoKeyed_ReturnsNull()
        {
            var provider = new ServiceCollection().BuildServiceProvider();
            var adapter = new TestableChannelAdapter(provider);

            var result = adapter.TestResolveFactory("unknown");

            Assert.Null(result);
        }

        [Fact]
        public void ResolveStreamingResponseFactory_RegistryWithNoMatch_ReturnsNull()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new StreamingResponseFactoryRegistry());

            var provider = services.BuildServiceProvider();
            var adapter = new TestableChannelAdapter(provider);

            var result = adapter.TestResolveFactory("nonexistent");

            Assert.Null(result);
        }

        private class TestableChannelAdapter : ChannelAdapter
        {
            public TestableChannelAdapter(IServiceProvider services)
            {
                Services = services;
            }

            public IStreamingResponseFactory TestResolveFactory(string channelId) => ResolveStreamingResponseFactory(channelId);

            public override Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
                => Task.FromResult(Array.Empty<ResourceResponse>());

            public override Task<ResourceResponse> UpdateActivityAsync(ITurnContext turnContext, IActivity activity, CancellationToken cancellationToken)
                => Task.FromResult<ResourceResponse>(null);

            public override Task DeleteActivityAsync(ITurnContext turnContext, ConversationReference reference, CancellationToken cancellationToken)
                => Task.CompletedTask;
        }
    }
}
