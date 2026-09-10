// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Adapters;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class AgentApplicationOptionsTests
    {
        [Fact]
        public void DiConstructor_CreatesRegistryAroundSuppliedAdapter()
        {
            var adapter = new Mock<IChannelAdapter>().Object;
            var services = new ServiceCollection();
            using var provider = services.BuildServiceProvider();
            var configuration = new ConfigurationBuilder().Build();

            var options = new AgentApplicationOptions(
                provider,
                configuration,
                adapter,
                new MemoryStorage());

            Assert.NotNull(options.ChannelAdapterRegistry);
            Assert.Same(adapter, options.ChannelAdapterRegistry.GetDefault());
        }
    }
}
