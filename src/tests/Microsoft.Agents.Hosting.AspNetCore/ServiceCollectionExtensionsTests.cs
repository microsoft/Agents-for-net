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
    }
}
