// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Microsoft.Agents.Builder.Tests
{
    public class StreamingResponseFactoryTests
    {
        [Fact]
        public void Registry_Register_And_TryGet_ReturnsFactory()
        {
            var registry = new StreamingResponseFactoryRegistry();
            var factory = new Mock<IStreamingResponseFactory>().Object;

            registry.Register("slack", factory);

            Assert.True(registry.TryGet("slack", out var result));
            Assert.Same(factory, result);
        }

        [Fact]
        public void Registry_TryGet_UnknownChannel_ReturnsFalse()
        {
            var registry = new StreamingResponseFactoryRegistry();

            Assert.False(registry.TryGet("unknown", out var result));
            Assert.Null(result);
        }

        [Fact]
        public void Registry_Register_OverwritesPrevious()
        {
            var registry = new StreamingResponseFactoryRegistry();
            var factory1 = new Mock<IStreamingResponseFactory>().Object;
            var factory2 = new Mock<IStreamingResponseFactory>().Object;

            registry.Register("slack", factory1);
            registry.Register("slack", factory2);

            registry.TryGet("slack", out var result);
            Assert.Same(factory2, result);
        }

        [Fact]
        public void AddStreamingResponseFactory_PopulatesRegistry()
        {
            var services = new ServiceCollection();
            services.AddStreamingResponseFactory<TestFactory>("slack");

            var provider = services.BuildServiceProvider();
            var registry = provider.GetRequiredService<StreamingResponseFactoryRegistry>();

            Assert.True(registry.TryGet("slack", out var factory));
            Assert.NotNull(factory);
        }

        [Fact]
        public void AddStreamingResponseFactory_TwoChannels_BothInRegistry()
        {
            var services = new ServiceCollection();
            services.AddStreamingResponseFactory<TestFactory>("slack");
            services.AddStreamingResponseFactory<TestFactory2>("teams");

            var provider = services.BuildServiceProvider();
            var registry = provider.GetRequiredService<StreamingResponseFactoryRegistry>();

            Assert.True(registry.TryGet("slack", out _));
            Assert.True(registry.TryGet("teams", out _));
        }

        [Fact]
        public void AddStreamingResponseFactory_InstanceOverload_Works()
        {
            var services = new ServiceCollection();
            var factory = new TestFactory();
            services.AddStreamingResponseFactory("slack", factory);

            var provider = services.BuildServiceProvider();
            var registry = provider.GetRequiredService<StreamingResponseFactoryRegistry>();

            Assert.True(registry.TryGet("slack", out var resolved));
            Assert.Same(factory, resolved);
        }

        private class TestFactory : IStreamingResponseFactory
        {
            public IStreamingResponse Create(ITurnContext ctx) => new Mock<IStreamingResponse>().Object;
        }
        private class TestFactory2 : IStreamingResponseFactory
        {
            public IStreamingResponse Create(ITurnContext ctx) => new Mock<IStreamingResponse>().Object;
        }
    }
}
