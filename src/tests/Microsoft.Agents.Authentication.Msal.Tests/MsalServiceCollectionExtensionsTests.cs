// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication.Msal.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Xunit;

namespace Microsoft.Agents.Authentication.Msal.Tests
{
    public class MsalServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddDefaultMsalAuth_RegistersMsalAuthConfigurationOptions()
        {
            var services = new ServiceCollection();
            var configuration = CreateConfiguration(new Dictionary<string, string>
            {
                { "MSALConfiguration:MSALEnabledLogPII", "true" },
                { "MSALConfiguration:MSALRetryCount", "5" },
                { "MSALConfiguration:MSALRequestTimeout", "00:00:45" },
            });

            services.AddDefaultMsalAuth(configuration);

            var serviceProvider = services.BuildServiceProvider();
            var options = serviceProvider.GetRequiredService<IOptions<MsalAuthConfigurationOptions>>().Value;

            Assert.True(options.MSALEnabledLogPII);
            Assert.Equal(5, options.MSALRetryCount);
            Assert.Equal(TimeSpan.FromSeconds(45), options.MSALRequestTimeout);
        }

        [Fact]
        public void AddDefaultMsalAuth_RegistersDefaultOptionsWhenSectionMissing()
        {
            var services = new ServiceCollection();
            var configuration = CreateConfiguration();

            services.AddDefaultMsalAuth(configuration);

            var serviceProvider = services.BuildServiceProvider();
            var options = serviceProvider.GetRequiredService<IOptions<MsalAuthConfigurationOptions>>().Value;

            Assert.False(options.MSALEnabledLogPII);
            Assert.Equal(3, options.MSALRetryCount);
            Assert.Equal(TimeSpan.FromSeconds(30), options.MSALRequestTimeout);
        }

        [Fact]
        public void AddDefaultMsalAuth_RegistersNamedHttpClient()
        {
            var services = new ServiceCollection();
            var configuration = CreateConfiguration();

            services.AddDefaultMsalAuth(configuration);

            var serviceProvider = services.BuildServiceProvider();
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

            var client = httpClientFactory.CreateClient("MSALClientFactory");

            Assert.NotNull(client);
        }

        [Fact]
        public void AddDefaultMsalAuth_RegistersRetryHandler()
        {
            var services = new ServiceCollection();

            services.AddDefaultMsalAuth(CreateConfiguration());

            var retryHandlerRegistration = services.SingleOrDefault(descriptor => descriptor.ServiceType == typeof(MSALHttpRetryHandlerHelper));

            Assert.NotNull(retryHandlerRegistration);
            Assert.Equal(ServiceLifetime.Transient, retryHandlerRegistration.Lifetime);
        }

        [Fact]
        public void AddDefaultMsalAuth_HttpClientHasConfiguredTimeout()
        {
            var services = new ServiceCollection();
            var configuration = CreateConfiguration(new Dictionary<string, string>
            {
                { "MSALConfiguration:MSALRequestTimeout", "00:01:15" },
            });

            services.AddDefaultMsalAuth(configuration);

            var serviceProvider = services.BuildServiceProvider();
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

            var client = httpClientFactory.CreateClient("MSALClientFactory");

            Assert.Equal(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(15), client.Timeout);
        }

        private static IConfiguration CreateConfiguration(Dictionary<string, string> settings = null)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
        }
    }
}
