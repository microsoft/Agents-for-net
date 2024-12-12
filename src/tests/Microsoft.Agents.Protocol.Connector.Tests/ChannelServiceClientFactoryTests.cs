// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Agents.Protocols.Connector;
using Microsoft.Agents.Authentication;
using Moq;
using System.Threading;
using Microsoft.Agents.Teams.Connector;
using System.Net.Http;
using Microsoft.Agents.Protocols.Primitives;

namespace Microsoft.Agents.Connector.Tests
{
    public class ChannelServiceClientFactoryTests
    {
        [Fact]
        public void ConstructionThrows()
        {
            var config = new ConfigurationRoot(new List<IConfigurationProvider>
            {
                new MemoryConfigurationProvider(new MemoryConfigurationSource
                {
                    InitialData = new Dictionary<string, string>
                    {
                        { "ConnectionsMap", string.Empty },
                    }
                })
            });

            var serviceProvider = new Mock<IServiceProvider>();
            var connections = new ConfigurationConnections(serviceProvider.Object, config);
            var httpFactory = new Mock<IHttpClientFactory>();

            // Null IConfiguration
            Assert.Throws<ArgumentNullException>(() => new RestChannelServiceClientFactory(null, httpFactory.Object, connections));

            // Null IConnections
            Assert.Throws<ArgumentNullException>(() => new RestChannelServiceClientFactory(config, httpFactory.Object, null));

            // Null IHttpClientFactory
            Assert.Throws<ArgumentNullException>(() => new RestChannelServiceClientFactory(config, null, connections));
        }

        [Fact]
        public async Task ConnectionMapNotFoundThrowsAsync()
        {
            var config = new ConfigurationRoot(new List<IConfigurationProvider>
            {
                new MemoryConfigurationProvider(new MemoryConfigurationSource
                {
                    InitialData = new Dictionary<string, string>
                    {
                        { "ConnectionsMap", string.Empty },
                    }
                })
            });
            
            var serviceProvider = new Mock<IServiceProvider>();
            var connections = new ConfigurationConnections(serviceProvider.Object, config);
            var httpFactory = new Mock<IHttpClientFactory>();

            var factory = new RestChannelServiceClientFactory(config, httpFactory.Object, connections);

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await factory.CreateConnectorClientAsync(new System.Security.Claims.ClaimsIdentity(), "http://serviceurl", string.Empty, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await factory.CreateUserTokenClientAsync(new System.Security.Claims.ClaimsIdentity(), CancellationToken.None));
        }

        [Fact]
        public async Task ConnectionMapNotFoundAnonymousDoesNotThrowAsync()
        {
            var config = new ConfigurationRoot(new List<IConfigurationProvider>
            {
                new MemoryConfigurationProvider(new MemoryConfigurationSource
                {
                    InitialData = new Dictionary<string, string>
                    {
                        { "ConnectionsMap", string.Empty },
                    }
                })
            });

            var serviceProvider = new Mock<IServiceProvider>();
            var connections = new ConfigurationConnections(serviceProvider.Object, config);
            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory.Setup(
                x => x.CreateClient(It.IsAny<string>()))
                .Returns(new Mock<HttpClient>().Object);

            var factory = new RestChannelServiceClientFactory(config, httpFactory.Object, connections);

            var connector = await factory.CreateConnectorClientAsync(new System.Security.Claims.ClaimsIdentity(), "http://serviceurl", string.Empty, CancellationToken.None, useAnonymous: true);
            Assert.IsType<RestTeamsConnectorClient>(connector);

            var tokeClient = await factory.CreateUserTokenClientAsync(new System.Security.Claims.ClaimsIdentity(), CancellationToken.None, useAnonymous: true);
            Assert.IsType<RestUserTokenClient>(tokeClient);
        }

        [Fact]
        public async Task ConnectionNotFoundThrowsAsync()
        {
            var config = new ConfigurationRoot(new List<IConfigurationProvider>
            {
                new MemoryConfigurationProvider(new MemoryConfigurationSource
                {
                    InitialData = new Dictionary<string, string>
                    {
                        { "ConnectionsMap:0:ServiceUrl", "*" },
                        { "ConnectionsMap:0:Connection", "BotServiceConnection" },
                    }
                })
            });

            var serviceProvider = new Mock<IServiceProvider>();
            var connections = new ConfigurationConnections(serviceProvider.Object, config);
            var httpFactory = new Mock<IHttpClientFactory>();

            var factory = new RestChannelServiceClientFactory(config, httpFactory.Object, connections);

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await factory.CreateConnectorClientAsync(new System.Security.Claims.ClaimsIdentity(), "http://serviceurl", string.Empty, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await factory.CreateUserTokenClientAsync(new System.Security.Claims.ClaimsIdentity(), CancellationToken.None));
        }

        [Fact]
        public async Task ConnectionFoundAsync()
        {
            var config = new ConfigurationRoot(new List<IConfigurationProvider>
            {
                new MemoryConfigurationProvider(new MemoryConfigurationSource
                {
                    InitialData = new Dictionary<string, string>
                    {
                        { "ConnectionsMap:0:ServiceUrl", "*" },
                        { "ConnectionsMap:0:Connection", "BotServiceConnection" },
                        { "Connections:BotServiceConnection:Type", "MsalAuth" },
                        { "Connections:BotServiceConnection:Assembly", "Microsoft.Agents.Authentication.Msal" },
                        { "Connections:BotServiceConnection:Settings:ClientId", "ClientId" },
                        { "Connections:BotServiceConnection:Settings:ClientSecret", "ClientSecret" },
                        { "Connections:BotServiceConnection:Settings:AuthorityEndpoint", "AuthorityEndpoint" },
                    }
                })
            });

            var serviceProvider = new Mock<IServiceProvider>();
            var connections = new ConfigurationConnections(serviceProvider.Object, config);
            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory.Setup(
                x => x.CreateClient(It.IsAny<string>()))
                .Returns(new Mock<HttpClient>().Object);

            var factory = new RestChannelServiceClientFactory(config, httpFactory.Object, connections);

            var connector = await factory.CreateConnectorClientAsync(new System.Security.Claims.ClaimsIdentity(), "http://serviceurl", string.Empty, CancellationToken.None, useAnonymous: true);
            Assert.IsType<RestTeamsConnectorClient>(connector);

            var tokeClient = await factory.CreateUserTokenClientAsync(new System.Security.Claims.ClaimsIdentity(), CancellationToken.None, useAnonymous: true);
            Assert.IsType<RestUserTokenClient>(tokeClient);
            Assert.Equal(new Uri(AuthenticationConstants.BotFrameworkOAuthUrl).ToString(), ((RestUserTokenClient)tokeClient).BaseUri.ToString());
        }

        [Fact]
        public async Task ConnectionFoundWithConfigTokenEndpointAsync()
        {
            var config = new ConfigurationRoot(new List<IConfigurationProvider>
            {
                new MemoryConfigurationProvider(new MemoryConfigurationSource
                {
                    InitialData = new Dictionary<string, string>
                    {
                        { "ConnectionsMap:0:ServiceUrl", "*" },
                        { "ConnectionsMap:0:Connection", "BotServiceConnection" },
                        { "Connections:BotServiceConnection:Type", "MsalAuth" },
                        { "Connections:BotServiceConnection:Assembly", "Microsoft.Agents.Authentication.Msal" },
                        { "Connections:BotServiceConnection:Settings:ClientId", "ClientId" },
                        { "Connections:BotServiceConnection:Settings:ClientSecret", "ClientSecret" },
                        { "Connections:BotServiceConnection:Settings:AuthorityEndpoint", "AuthorityEndpoint" },
                        { "RestChannelServiceClientFactory:TokenServiceEndpoint", "https://test.token.endpoint" }
                    }
                })
            });

            var serviceProvider = new Mock<IServiceProvider>();
            var connections = new ConfigurationConnections(serviceProvider.Object, config);
            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory.Setup(
                x => x.CreateClient(It.IsAny<string>()))
                .Returns(new Mock<HttpClient>().Object);

            var factory = new RestChannelServiceClientFactory(config, httpFactory.Object, connections);

            var connector = await factory.CreateConnectorClientAsync(new System.Security.Claims.ClaimsIdentity(), "http://serviceurl", string.Empty, CancellationToken.None, useAnonymous: true);
            Assert.IsType<RestTeamsConnectorClient>(connector);

            var tokeClient = await factory.CreateUserTokenClientAsync(new System.Security.Claims.ClaimsIdentity(), CancellationToken.None, useAnonymous: true);
            Assert.IsType<RestUserTokenClient>(tokeClient);
            Assert.Equal("https://test.token.endpoint/", ((RestUserTokenClient)tokeClient).BaseUri.ToString());
        }
    }
}
