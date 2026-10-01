// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.Adapters;
using Microsoft.Agents.Builder.Compat;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace Microsoft.Agents.Hosting.AspNetCore.Tests
{
    public class ServiceCollectionExtensionsTests
    {
        private sealed class CustomCloudAdapter(
            IChannelServiceClientFactory channelServiceClientFactory,
            IActivityTaskQueue activityTaskQueue)
            : CloudAdapter(channelServiceClientFactory, activityTaskQueue)
        {
        }

        [Fact]
        public void AddCloudAdapter_ShouldSetServices()
        {
            var collection = new ServiceCollection();
            collection.AddCloudAdapter();

            var services = collection
                .Select(e => e.ImplementationType ?? e.ServiceType)
                .ToList();
            var expected = new List<Type>{
                typeof(HostedActivityServiceOptions),
                typeof(HostedTaskServiceOptions),
                typeof(HostedActivityService),
                typeof(HostedTaskService),
                typeof(BackgroundTaskQueue),
                typeof(ActivityTaskQueue),
                typeof(CloudAdapter), // Default Type passed to AddCloudAdapter.
                typeof(IAgentHttpAdapter),
                typeof(ChannelAdapterRegistry), // IChannelAdapterRegistry.
                typeof(IChannelAdapter),
            };

            Assert.Equal(expected, services);
        }

        [Fact]
        public async Task AddCloudAdapter_ShouldUseRegisteredErrorHandler()
        {
            var services = new ServiceCollection();
            var channelServiceClientFactory = new Mock<IChannelServiceClientFactory>();
            var errorHandler = new Mock<ICloudAdapterErrorHandler>();
            var turnContext = new Mock<ITurnContext>();
            var exception = new InvalidOperationException("test");
            errorHandler
                .Setup(handler => handler.HandleTurnErrorAsync(turnContext.Object, exception))
                .Returns(Task.CompletedTask)
                .Verifiable(Times.Once);
            services.AddSingleton(channelServiceClientFactory.Object);
            services.AddSingleton(errorHandler.Object);
            services.AddLogging();
            services.AddCloudAdapter();

            using var provider = services.BuildServiceProvider();
            var adapter = provider.GetRequiredService<CloudAdapter>();

            await adapter.OnTurnError(turnContext.Object, exception);

            errorHandler.Verify();
        }

        [Fact]
        public void AddAsyncAdapterSupport_ShouldRegisterHostedServiceOptionsOnce()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["HostedActivityServiceOptions:ShutdownTimeoutSeconds"] = "23",
                    ["HostedTaskServiceOptions:ShutdownTimeoutSeconds"] = "29"
                })
                .Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddLogging();

            services.AddAsyncAdapterSupport();
            services.AddAsyncAdapterSupport();

            using var provider = services.BuildServiceProvider();
            var activityOptions = provider.GetRequiredService<HostedActivityServiceOptions>();
            var taskOptions = provider.GetRequiredService<HostedTaskServiceOptions>();
            var hostedServices = provider.GetServices<IHostedService>().ToList();

            Assert.Equal(23, activityOptions.ShutdownTimeoutSeconds);
            Assert.Equal(29, taskOptions.ShutdownTimeoutSeconds);
            Assert.Collection(
                hostedServices,
                service => Assert.IsType<HostedActivityService>(service),
                service => Assert.IsType<HostedTaskService>(service));
            Assert.Single(services, service => service.ServiceType == typeof(HostedActivityServiceOptions));
            Assert.Single(services, service => service.ServiceType == typeof(HostedTaskServiceOptions));
        }

        [Fact]
        public void AddBot_ShouldSetServices()
        {
            var builder = new Mock<IHostApplicationBuilder>();
            builder.SetupGet(e => e.Services).Returns(new ServiceCollection());
            AgentHostExtensions.AddAgent<ActivityHandler>(builder.Object);

            var services = builder.Object.Services
                .Select(e => e.ImplementationType ?? e.ServiceType)
                .ToList();
            var expected = new List<Type>{
                typeof(ConfigurationConnections),
                typeof(RestChannelServiceClientFactory),
                typeof(IOutboundHostValidator),
                // CloudAdapter services.
                typeof(HostedActivityServiceOptions),
                typeof(HostedTaskServiceOptions),
                typeof(HostedActivityService),
                typeof(HostedTaskService),
                typeof(BackgroundTaskQueue),
                typeof(ActivityTaskQueue),
                typeof(CloudAdapter),
                typeof(IAgentHttpAdapter),
                typeof(ChannelAdapterRegistry), // IChannelAdapterRegistry.
                typeof(IChannelAdapter),
                typeof(TestAgentExtensionService),
                typeof(ActivityHandler), // IAgent.
                typeof(ActivityHandler), // TAgent.
            };

            Assert.Equal(expected, services);
        }

        [Fact]
        public void AddAgentCore_WithCustomConfigurationSections_UsesConfiguredConnectionsAndMap()
        {
            const string connectionsKey = "Agent:Authentication:Connections";
            const string mapKey = "Agent:Authentication:ConnectionsMap";
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    [$"{connectionsKey}:FirstConnection:Type"] = "MsalAuth",
                    [$"{connectionsKey}:FirstConnection:Assembly"] = "Microsoft.Agents.Authentication.Msal",
                    [$"{connectionsKey}:FirstConnection:Settings:ClientId"] = "first-client-id",
                    [$"{connectionsKey}:FirstConnection:Settings:ClientSecret"] = "first-client-secret",
                    [$"{connectionsKey}:FirstConnection:Settings:TenantId"] = "first-tenant-id",
                    [$"{connectionsKey}:SecondConnection:Type"] = "MsalAuth",
                    [$"{connectionsKey}:SecondConnection:Assembly"] = "Microsoft.Agents.Authentication.Msal",
                    [$"{connectionsKey}:SecondConnection:Settings:ClientId"] = "second-client-id",
                    [$"{connectionsKey}:SecondConnection:Settings:ClientSecret"] = "second-client-secret",
                    [$"{connectionsKey}:SecondConnection:Settings:TenantId"] = "second-tenant-id",
                    [$"{mapKey}:0:ServiceUrl"] = "*",
                    [$"{mapKey}:0:Connection"] = "SecondConnection",
                })
                .Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddLogging();

            services.AddAgentCore<CloudAdapter>(connectionsKey, mapKey);
            services.AddAgent<ActivityHandler, CloudAdapter>();

            using var provider = services.BuildServiceProvider();
            var connections = provider.GetRequiredService<IConnections>();

            Assert.Equal("first-client-id", connections.GetConnection("FirstConnection").ConnectionSettings.ClientId);
            Assert.Equal("second-client-id", connections.GetDefaultConnection().ConnectionSettings.ClientId);
            Assert.Same(connections, provider.GetRequiredService<IConnections>());
            Assert.Single(services, service => service.ServiceType == typeof(IConnections));
        }

        [Fact]
        public void AddAgentCore_WithCustomConfigurationSections_PreservesExistingConnections()
        {
            var expected = new Mock<IConnections>().Object;
            var services = new ServiceCollection();
            services.AddSingleton(expected);

            services.AddAgentCore<CloudAdapter>("Agent:Connections", "Agent:ConnectionsMap");

            using var provider = services.BuildServiceProvider();
            Assert.Same(expected, provider.GetRequiredService<IConnections>());
            Assert.Single(services, service => service.ServiceType == typeof(IConnections));
        }

        [Fact]
        public void AddAgentCore_WithCustomConfigurationSections_ReturnsHostBuilder()
        {
            var builder = new Mock<IHostApplicationBuilder>();
            builder.SetupGet(instance => instance.Services).Returns(new ServiceCollection());

            var result = AgentHostExtensions.AddAgentCore(
                builder.Object,
                "Agent:Connections",
                "Agent:ConnectionsMap");

            Assert.Same(builder.Object, result);
            Assert.Single(builder.Object.Services, service => service.ServiceType == typeof(IConnections));
        }

        [Fact]
        public void AddAgentCore_WithCustomAdapterAndConfigurationSections_ReturnsHostBuilder()
        {
            var builder = new Mock<IHostApplicationBuilder>();
            builder.SetupGet(instance => instance.Services).Returns(new ServiceCollection());

            var result = AgentHostExtensions.AddAgentCore<CustomCloudAdapter>(
                builder.Object,
                "Agent:Connections",
                "Agent:ConnectionsMap");

            Assert.Same(builder.Object, result);
            Assert.Single(builder.Object.Services, service => service.ServiceType == typeof(IConnections));
            Assert.Single(builder.Object.Services, service => service.ServiceType == typeof(CustomCloudAdapter));
        }

        [Theory]
        [InlineData(null, "Agent:ConnectionsMap")]
        [InlineData("", "Agent:ConnectionsMap")]
        [InlineData(" ", "Agent:ConnectionsMap")]
        [InlineData("Agent:Connections", null)]
        [InlineData("Agent:Connections", "")]
        [InlineData("Agent:Connections", " ")]
        public void AddAgentCore_WithInvalidConfigurationSection_Throws(string connectionsKey, string mapKey)
        {
            var services = new ServiceCollection();

            Assert.ThrowsAny<ArgumentException>(() =>
                services.AddAgentCore<CloudAdapter>(connectionsKey, mapKey));
        }
    }
}
