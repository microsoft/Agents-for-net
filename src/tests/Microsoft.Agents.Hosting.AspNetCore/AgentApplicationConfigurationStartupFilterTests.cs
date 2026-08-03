// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.Agents.Hosting.AspNetCore.Tests
{
    public class AgentApplicationConfigurationStartupFilterTests
    {
        [Fact]
        public void AddAgentApplicationOptions_RegistersStartupFilterOnce()
        {
            var services = new ServiceCollection();

            services.AddAgentApplicationOptions();
            services.AddAgentApplicationOptions();

            Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IStartupFilter));
        }

        [Fact]
        public void Configure_InvalidUserAuthorization_Throws()
        {
            var filter = CreateStartupFilter(CreateConfiguration(new Dictionary<string, string>
            {
                { "AgentApplication:UserAuthorization:DefaultHandlerName", "NotFound" },
                { "AgentApplication:UserAuthorization:Handlers:auto:Settings:AzureBotOAuthConnectionName", "auto-connection" }
            }));

            var exception = Assert.Throws<IndexOutOfRangeException>(() => filter.Configure(_ => { }));

            Assert.Contains("NotFound", exception.Message);
        }

        [Fact]
        public void Configure_ValidUserAuthorization_CallsNext()
        {
            var filter = CreateStartupFilter(CreateConfiguration(new Dictionary<string, string>
            {
                { "AgentApplication:UserAuthorization:DefaultHandlerName", "auto" },
                { "AgentApplication:UserAuthorization:Handlers:auto:Settings:AzureBotOAuthConnectionName", "auto-connection" }
            }));

            var called = false;
            Action<IApplicationBuilder> next = _ => called = true;

            filter.Configure(next)(new Mock<IApplicationBuilder>().Object);

            Assert.True(called);
        }

        [Fact]
        public void Configure_UserAuthorizationOptionsFromDI_SkipsConfigurationValidation()
        {
            // AgentApplicationOptions uses a DI'd UserAuthorizationOptions instead of the configuration section.
            var userAuthorizationOptions = new UserAuthorizationOptions(
                NullLoggerFactory.Instance,
                new MemoryStorage(),
                new Mock<IConnections>().Object,
                CreateHandler("auto"));

            var filter = CreateStartupFilter(
                CreateConfiguration(new Dictionary<string, string>
                {
                    { "AgentApplication:UserAuthorization:DefaultHandlerName", "NotFound" },
                    { "AgentApplication:UserAuthorization:Handlers:auto:Settings:AzureBotOAuthConnectionName", "auto-connection" }
                }),
                services => services.AddSingleton(userAuthorizationOptions));

            filter.Configure(_ => { });
        }

        private static IUserAuthorization CreateHandler(string name)
        {
            var handler = new Mock<IUserAuthorization>();
            handler.SetupGet(e => e.Name).Returns(name);
            return handler.Object;
        }

        private static IStartupFilter CreateStartupFilter(IConfiguration configuration, Action<IServiceCollection> configureServices = null)
        {
            var services = new ServiceCollection();
            services.AddSingleton(configuration);
            services.AddAgentApplicationOptions();
            configureServices?.Invoke(services);

            return services.BuildServiceProvider().GetServices<IStartupFilter>().Single();
        }

        private static IConfiguration CreateConfiguration(Dictionary<string, string> settings)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
        }
    }
}
