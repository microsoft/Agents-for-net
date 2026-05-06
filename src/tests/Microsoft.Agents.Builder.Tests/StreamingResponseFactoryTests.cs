// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
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
    }
}
