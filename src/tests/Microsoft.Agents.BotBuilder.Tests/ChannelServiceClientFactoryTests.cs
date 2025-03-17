// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Agents.Authentication;
using Moq;
using System.Threading;
using System.Net.Http;
using Microsoft.Agents.Connector;
using Xunit.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Errors;
using System.Text;
using Microsoft.Agents.TestSupport;
using Microsoft.Agents.BotBuilder.Errors;
using Azure;
using Microsoft.Agents.Core;

namespace Microsoft.Agents.BotBuilder.Tests
{
    public class ChannelServiceClientFactoryTests(ITestOutputHelper output)
    {
        readonly ITestOutputHelper _outputListener = output;

        [Fact]
        public void ConstructionThrows()
        {
            var serviceProvider = TestSupport.ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: new Dictionary<string, string>
                    {
                        { "ConnectionsMap", string.Empty },
                    });

            //var serviceProvider = new Mock<IServiceProvider>();
            var connections = new ConfigurationConnections(serviceProvider, serviceProvider.GetService<IConfiguration>());
            var httpFactory = new Mock<IHttpClientFactory>();

            // Null IConfiguration
            Assert.Throws<ArgumentNullException>(() => new RestChannelServiceClientFactory(null, httpFactory.Object, connections));

            // Null IConnections
            Assert.Throws<ArgumentNullException>(() => new RestChannelServiceClientFactory(serviceProvider.GetService<IConfiguration>(), httpFactory.Object, null));

            // Null IHttpClientFactory
            Assert.Throws<ArgumentNullException>(() => new RestChannelServiceClientFactory(serviceProvider.GetService<IConfiguration>(), null, connections));
        }

        [Fact]
        public async Task ConnectionMapNotFoundThrowsAsync()
        {
            //_ = new Core.RequestContext(new TestPropagation());
                
            var serviceProvider = TestSupport.ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: new Dictionary<string, string>
                    {
                        { "ConnectionsMap", string.Empty },
                    });

            var connections = new ConfigurationConnections(serviceProvider, serviceProvider.GetService<IConfiguration>());
            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory
                .Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(new Mock<HttpClient>().Object);
            var traceActivity = Activity.CreateTraceActivity("Test");
            traceActivity.Conversation = new ConversationAccount(id: "1234");

            var factory = new RestChannelServiceClientFactory(serviceProvider.GetService<IConfiguration>(), httpFactory.Object, connections);

            try
            {
                IConnectorClient v = await factory.CreateConnectorClientAsync(new System.Security.Claims.ClaimsIdentity(), "http://serviceurl", "audience", CancellationToken.None);
                await v.Conversations.SendToConversationAsync(traceActivity, CancellationToken.None);
            }
            catch (Exception e)
            {
                ExceptionTester.IsException<OperationCanceledException>(e, ErrorHelper.NullIAccessTokenProvider.code, _outputListener);
            }

            try
            {

                IConnectorClient v = await factory.CreateConnectorClientAsync(new System.Security.Claims.ClaimsIdentity(), "http://serviceurl", "audience", CancellationToken.None);
                await v.Conversations.SendToConversationAsync(traceActivity, CancellationToken.None);
            }
            catch (Exception e)
            {
                ExceptionTester.IsException<OperationCanceledException>(e, ErrorHelper.NullIAccessTokenProvider.code, _outputListener);
            }

            try
            {
                IUserTokenClient u = await factory.CreateUserTokenClientAsync(new System.Security.Claims.ClaimsIdentity(), CancellationToken.None);
                await u.GetUserTokenAsync(userId: "ABC", connectionName: "ConnNAM", channelId: "TEST", magicCode: "Im Magic", CancellationToken.None);
            }
            catch (Exception e)
            {
                ExceptionTester.IsException<OperationCanceledException>(e, ErrorHelper.NullUserTokenProviderIAccessTokenProvider.code, _outputListener);
            }

        }


        [Fact]
        public async Task ConnectionMapNotFoundAnonymousDoesNotThrowAsync()
        {
            var serviceProvider = TestSupport.ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: new Dictionary<string, string>
                    {
                        { "ConnectionsMap", string.Empty },
                    });

            var connections = new ConfigurationConnections(serviceProvider, serviceProvider.GetService<IConfiguration>());
            var httpFactory = new Mock<IHttpClientFactory>();

            var traceActivity = Activity.CreateTraceActivity("Test");
            traceActivity.Conversation = new ConversationAccount(id: "1234");
            traceActivity.ChannelId = "Emulator";

            var audience = "http://localhost";

            httpFactory.Setup(
                x => x.CreateClient(It.IsAny<string>()))
                .Returns(new Mock<HttpClient>().Object);

            var factory = new RestChannelServiceClientFactory(serviceProvider.GetService<IConfiguration>(), httpFactory.Object, connections);

            var connector = await factory.CreateConnectorClientAsync(new System.Security.Claims.ClaimsIdentity(), "http://serviceurl", audience, CancellationToken.None, useAnonymous: true);
            Assert.IsType<RestConnectorClient>(connector);

            var tokeClient = await factory.CreateUserTokenClientAsync(new System.Security.Claims.ClaimsIdentity(), CancellationToken.None, useAnonymous: true);
            Assert.IsType<RestUserTokenClient>(tokeClient);
        }

        [Fact]
        public async Task ConnectionNotFoundThrowsAsync()
        {

            var serviceProvider = TestSupport.ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: new Dictionary<string, string>
                    {
                         { "ConnectionsMap:0:ServiceUrl", "*" },
                        { "ConnectionsMap:0:Connection", "BotServiceConnection" },
                    });

            var connections = new ConfigurationConnections(serviceProvider, serviceProvider.GetService<IConfiguration>());
            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory
                .Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(new Mock<HttpClient>().Object);
            var traceActivity = Activity.CreateTraceActivity("Test");
            traceActivity.Conversation = new ConversationAccount(id: "1234");

            var factory = new RestChannelServiceClientFactory(serviceProvider.GetService<IConfiguration>(), httpFactory.Object, connections);

            try
            {
                IConnectorClient v = await factory.CreateConnectorClientAsync(new System.Security.Claims.ClaimsIdentity(), "http://serviceurl", "audience", CancellationToken.None);
                await v.Conversations.SendToConversationAsync(traceActivity, CancellationToken.None);
            }
            catch (Exception e)
            {
                ExceptionTester.IsException<OperationCanceledException>(e, ErrorHelper.NullIAccessTokenProvider.code, _outputListener);
            }


            try
            {
                IUserTokenClient u = await factory.CreateUserTokenClientAsync(new System.Security.Claims.ClaimsIdentity(), CancellationToken.None);
                await u.GetUserTokenAsync(userId: "ABC", connectionName: "ConnNAM", channelId: "TEST", magicCode: "Im Magic", CancellationToken.None);
            }
            catch (Exception e)
            {
                ExceptionTester.IsException<OperationCanceledException>(e, ErrorHelper.NullUserTokenProviderIAccessTokenProvider.code, _outputListener);
            }
            //await Assert.ThrowsAsync<InvalidOperationException>(async () => await factory.CreateConnectorClientAsync(new System.Security.Claims.ClaimsIdentity(), "http://serviceurl", "audience", CancellationToken.None));
            //await Assert.ThrowsAsync<InvalidOperationException>(async () => await factory.CreateUserTokenClientAsync(new System.Security.Claims.ClaimsIdentity(), CancellationToken.None));
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

            var connector = await factory.CreateConnectorClientAsync(new System.Security.Claims.ClaimsIdentity(), "http://serviceurl", "audience", CancellationToken.None, useAnonymous: true);
            Assert.IsType<RestConnectorClient>(connector);

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

            var connector = await factory.CreateConnectorClientAsync(new System.Security.Claims.ClaimsIdentity(), "http://serviceurl", "audience", CancellationToken.None, useAnonymous: true);
            Assert.IsType<RestConnectorClient>(connector);

            var tokeClient = await factory.CreateUserTokenClientAsync(new System.Security.Claims.ClaimsIdentity(), CancellationToken.None, useAnonymous: true);
            Assert.IsType<RestUserTokenClient>(tokeClient);
            Assert.Equal("https://test.token.endpoint/", ((RestUserTokenClient)tokeClient).BaseUri.ToString());
        }
    }

    class TestPropagation() : IHeaderPropagation
    {
        public Dictionary<string, string> Headers => [];

        public string UserAgent => null;
    }
}
