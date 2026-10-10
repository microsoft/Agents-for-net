// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.UserAuth.TokenService;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.Hosting.AspNetCore.Tests
{
    public class AgentConfigurationValidationHostedServiceTests
    {
        [Fact]
        public async Task StartingAsync_InDevelopment_ThrowsForUnknownDefaultHandler()
        {
            using var serviceProvider = CreateServiceProvider(new Dictionary<string, string>
            {
                ["AgentApplication:UserAuthorization:DefaultHandlerName"] = "not-found",
                ["AgentApplication:UserAuthorization:Handlers:graph:Settings:AzureBotOAuthConnectionName"] = "graph",
            });

            var service = new AgentConfigurationValidationHostedService(
                serviceProvider,
                CreateEnvironment(Environments.Development));

            await Assert.ThrowsAsync<IndexOutOfRangeException>(
                () => service.StartingAsync(CancellationToken.None));
        }

        [Fact]
        public async Task HostStartAsync_InDevelopment_ThrowsForUnknownDefaultHandler()
        {
            using var host = new HostBuilder()
                .UseEnvironment(Environments.Development)
                .ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["AgentApplication:UserAuthorization:DefaultHandlerName"] = "not-found",
                    ["AgentApplication:UserAuthorization:Handlers:graph:Settings:AzureBotOAuthConnectionName"] = "graph",
                }))
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IStorage, MemoryStorage>();
                    services.AddSingleton<IConnections>(Mock.Of<IConnections>());
                    services.AddSingleton<IChannelAdapter>(Mock.Of<IChannelAdapter>());
                    services.AddAgentApplicationOptions();
                })
                .Build();

            await Assert.ThrowsAsync<IndexOutOfRangeException>(
                () => host.StartAsync(CancellationToken.None));
        }

        [Fact]
        public async Task StartingAsync_InDevelopment_ThrowsWhenAzureBotUserAuthorizationConnectionNameIsMissing()
        {
            using var serviceProvider = CreateServiceProvider(new Dictionary<string, string>
            {
                ["AgentApplication:UserAuthorization:DefaultHandlerName"] = "graph",
                ["AgentApplication:UserAuthorization:Handlers:graph:Settings:Title"] = "Sign in",
            });
            var service = new AgentConfigurationValidationHostedService(
                serviceProvider,
                CreateEnvironment(Environments.Development));

            await Assert.ThrowsAsync<ArgumentException>(
                () => service.StartingAsync(CancellationToken.None));
        }

        [Fact]
        public async Task StartingAsync_InDevelopment_ThrowsWhenProgrammaticAzureBotUserAuthorizationConnectionNameIsMissing()
        {
            var storage = new MemoryStorage();
            var connections = Mock.Of<IConnections>();
            var options = new AgentApplicationOptions(storage)
            {
                UserAuthorization = new UserAuthorizationOptions(
                    NullLoggerFactory.Instance,
                    storage,
                    connections,
                    new AzureBotUserAuthorization("graph", storage, connections, new OAuthSettings()))
                {
                    DefaultHandlerName = "graph",
                },
            };
            using var serviceProvider = new ServiceCollection()
                .AddSingleton(options)
                .BuildServiceProvider();
            var service = new AgentConfigurationValidationHostedService(
                serviceProvider,
                CreateEnvironment(Environments.Development));

            await Assert.ThrowsAsync<ArgumentException>(
                () => service.StartingAsync(CancellationToken.None));
        }

        [Fact]
        public async Task StartingAsync_InDevelopment_ThrowsWhenUserAuthorizationHandlersAreMissing()
        {
            using var serviceProvider = CreateServiceProvider(new Dictionary<string, string>
            {
                ["AgentApplication:UserAuthorization:DefaultHandlerName"] = "graph",
            });
            var service = new AgentConfigurationValidationHostedService(
                serviceProvider,
                CreateEnvironment(Environments.Development));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.StartingAsync(CancellationToken.None));

            Assert.Equal(-50012, exception.HResult);
        }

        [Fact]
        public async Task StartingAsync_InDevelopment_ThrowsForEmptyAdaptiveCardActionSubmitFilter()
        {
            using var serviceProvider = CreateServiceProvider(new Dictionary<string, string>
            {
                ["AgentApplication:AdaptiveCards:ActionSubmitFilter"] = " ",
            });
            var service = new AgentConfigurationValidationHostedService(
                serviceProvider,
                CreateEnvironment(Environments.Development));

            await Assert.ThrowsAsync<ArgumentException>(
                () => service.StartingAsync(CancellationToken.None));
        }

        [Theory]
        [InlineData(-1, 2000)]
        [InlineData(500, -1)]
        public async Task StartingAsync_InDevelopment_ThrowsForNegativeTypingTiming(int initialDelayMs, int intervalMs)
        {
            var options = new AgentApplicationOptions(new MemoryStorage())
            {
                StartTypingTimer = true,
                TypingOptions = new TypingOptions
                {
                    InitialDelayMs = initialDelayMs,
                    IntervalMs = intervalMs,
                },
            };
            using var serviceProvider = new ServiceCollection()
                .AddSingleton(options)
                .BuildServiceProvider();
            var service = new AgentConfigurationValidationHostedService(
                serviceProvider,
                CreateEnvironment(Environments.Development));

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => service.StartingAsync(CancellationToken.None));
        }

        [Fact]
        public async Task StartingAsync_InDevelopment_ThrowsForNegativeChannelTypingTiming()
        {
            var options = new AgentApplicationOptions(new MemoryStorage())
            {
                StartTypingTimer = true,
                TypingOptions = new TypingOptions
                {
                    ChannelStrategies = new Dictionary<string, ITypingChannelStrategy>
                    {
                        ["test"] = new TypingChannelStrategy(-1, 1000),
                    },
                },
            };
            using var serviceProvider = new ServiceCollection()
                .AddSingleton(options)
                .BuildServiceProvider();
            var service = new AgentConfigurationValidationHostedService(
                serviceProvider,
                CreateEnvironment(Environments.Development));

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => service.StartingAsync(CancellationToken.None));
        }

        [Fact]
        public async Task StartingAsync_InDevelopment_ThrowsForConnectionMapReferenceThatDoesNotExist()
        {
            using var serviceProvider = CreateServiceProvider(new Dictionary<string, string>
            {
                ["Connections:primary:Settings:ClientId"] = "client-id",
                ["ConnectionsMap:0:ServiceUrl"] = "*",
                ["ConnectionsMap:0:Connection"] = "missing",
            }, useConfigurationConnections: true);
            var service = new AgentConfigurationValidationHostedService(
                serviceProvider,
                CreateEnvironment(Environments.Development));

            await Assert.ThrowsAsync<IndexOutOfRangeException>(
                () => service.StartingAsync(CancellationToken.None));
        }

        [Fact]
        public async Task StartingAsync_InDevelopment_ThrowsForInvalidConnectionMapPattern()
        {
            using var serviceProvider = CreateServiceProvider(new Dictionary<string, string>
            {
                ["Connections:primary:Settings:ClientId"] = "client-id",
                ["ConnectionsMap:0:ServiceUrl"] = "[",
                ["ConnectionsMap:0:Connection"] = "primary",
            }, useConfigurationConnections: true);
            var service = new AgentConfigurationValidationHostedService(
                serviceProvider,
                CreateEnvironment(Environments.Development));

            await Assert.ThrowsAsync<RegexParseException>(
                () => service.StartingAsync(CancellationToken.None));
        }

        [Theory]
        [InlineData(typeof(HostedActivityServiceOptions), "HostedActivityServiceOptions:ShutdownTimeoutSeconds")]
        [InlineData(typeof(HostedTaskServiceOptions), "HostedTaskServiceOptions:ShutdownTimeoutSeconds")]
        public async Task StartingAsync_InDevelopment_ThrowsForNegativeShutdownTimeout(Type optionsType, string configurationKey)
        {
            var configurationValues = new Dictionary<string, string>
            {
                [configurationKey] = "-1",
            };
            var services = new ServiceCollection();

            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(configurationValues).Build());
            services.AddSingleton<IStorage, MemoryStorage>();
            services.AddSingleton<IConnections>(Mock.Of<IConnections>());
            services.AddSingleton<IChannelAdapter>(Mock.Of<IChannelAdapter>());
            services.AddLogging();
            services.AddAgentApplicationOptions();
            services.AddSingleton(optionsType);

            using var timeoutServiceProvider = services.BuildServiceProvider();
            var service = new AgentConfigurationValidationHostedService(
                timeoutServiceProvider,
                CreateEnvironment(Environments.Development));

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => service.StartingAsync(CancellationToken.None));
        }

        [Fact]
        public async Task StartingAsync_OutsideDevelopment_DoesNotResolveAgentOptions()
        {
            var services = new ServiceCollection();
            services.AddAgentApplicationOptions();

            using var serviceProvider = services.BuildServiceProvider();
            var service = new AgentConfigurationValidationHostedService(
                serviceProvider,
                CreateEnvironment(Environments.Production));

            await service.StartingAsync(CancellationToken.None);
        }

        [Fact]
        public void AddAgentApplicationOptions_RegistersValidationServiceOnce()
        {
            var services = new ServiceCollection();

            services.AddAgentApplicationOptions();
            services.AddAgentApplicationOptions();

            Assert.Single(services, descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(AgentConfigurationValidationHostedService));
        }

        private static ServiceProvider CreateServiceProvider(
            IDictionary<string, string> configurationValues,
            bool useConfigurationConnections = false)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configurationValues)
                .Build();
            var services = new ServiceCollection();

            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton<IStorage, MemoryStorage>();
            if (useConfigurationConnections)
            {
                services.AddSingleton<IConnections, ConfigurationConnections>();
            }
            else
            {
                services.AddSingleton<IConnections>(Mock.Of<IConnections>());
            }
            services.AddSingleton<IChannelAdapter>(Mock.Of<IChannelAdapter>());
            services.AddLogging();
            services.AddAgentApplicationOptions();

            return services.BuildServiceProvider();
        }

        private static IHostEnvironment CreateEnvironment(string environmentName)
        {
            var environment = new Mock<IHostEnvironment>();
            environment.SetupGet(value => value.EnvironmentName).Returns(environmentName);
            return environment.Object;
        }
    }
}
