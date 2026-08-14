// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.Adapters;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

[assembly: Microsoft.Agents.Builder.AgentServiceRegistrationAttribute(
    typeof(Microsoft.Agents.Hosting.AspNetCore.Tests.TestAgentServiceRegistrar))]

namespace Microsoft.Agents.Hosting.AspNetCore.Tests
{
    public sealed class TestAgentExtensionService
    {
    }

    public sealed class TestAgentServiceRegistrar : IAgentServiceRegistrar
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.TryAddSingleton<TestAgentExtensionService>();
        }
    }

    public class AgentServiceRegistrationTests
    {
        private sealed class AlternateAdapter : ChannelAdapter
        {
            public override Task<ResourceResponse[]> SendActivitiesAsync(
                ITurnContext turnContext,
                IActivity[] activities,
                CancellationToken cancellationToken)
                => Task.FromResult(Array.Empty<ResourceResponse>());
        }

        [Fact]
        public void AddAgentCore_AppliesExtensionRegistrationOnce()
        {
            var services = new ServiceCollection();

            services.AddAgentCore<CloudAdapter>();
            services.AddAgentCore<CloudAdapter>();

            Assert.Single(services, descriptor => descriptor.ServiceType == typeof(TestAgentExtensionService));
        }

        [Fact]
        public void ExplicitDefaultBeforeAddCloudAdapter_IsPreserved()
        {
            var services = new ServiceCollection();
            services.SetDefaultChannelAdapter<AlternateAdapter>();

            services.AddCloudAdapter();

            using var provider = services.BuildServiceProvider();
            Assert.IsType<AlternateAdapter>(provider.GetRequiredService<IChannelAdapter>());
        }

        [Fact]
        public void ExplicitDefaultAfterAddCloudAdapter_ReplacesCloudAdapter()
        {
            var services = new ServiceCollection();
            services.AddCloudAdapter();

            services.SetDefaultChannelAdapter<AlternateAdapter>();

            using var provider = services.BuildServiceProvider();
            Assert.IsType<AlternateAdapter>(provider.GetRequiredService<IChannelAdapter>());
        }

        [Fact]
        public void DirectIChannelAdapterOverride_RemainsRegistryDefault()
        {
            var services = new ServiceCollection();
            services.AddCloudAdapter();
            services.AddSingleton<IChannelAdapter, AlternateAdapter>();

            using var provider = services.BuildServiceProvider();
            var registry = provider.GetRequiredService<IChannelAdapterRegistry>();

            Assert.IsType<AlternateAdapter>(registry.GetDefault());
        }
    }
}
