// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Authentication.Errors;
using Microsoft.Agents.Authentication.Model;
using Microsoft.Agents.Authentication.Msal.Model;
using Microsoft.Agents.Authentication.Msal;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
#if !NETFRAMEWORK
using System.Runtime.Loader;
#endif
using System.Security.Claims;
using Xunit;
using Xunit.Abstractions;
using Moq;

namespace Microsoft.Agents.Auth.Tests
{
    public class ConfigurationConnectionsTests(ITestOutputHelper output)
    {
        private readonly ClaimsIdentity _identity = new([]);
        readonly ITestOutputHelper _outputListener = output;

        [Fact]
        public void GetConnection_ShouldReturnAccessTokenProviderWithConnectionName()
        {

            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: new Dictionary<string, string>
                    {
                        { "ConnectionsMap:0:ServiceUrl", "*" },
                        { "ConnectionsMap:0:Connection", "ServiceConnection" },
                        { "Connections:ServiceConnection:Type", "MsalAuth" },
                        { "Connections:ServiceConnection:Assembly", "Microsoft.Agents.Authentication.Msal" },
                        { "Connections:ServiceConnection:Settings:ClientId", "ClientId" },
                        { "Connections:ServiceConnection:Settings:ClientSecret", "ClientSecret" },
                        { "Connections:ServiceConnection:Settings:AuthorityEndpoint", "AuthorityEndpoint" },
                    });

            var configurationConnections = new ConfigurationConnections(serviceProvider, serviceProvider.GetService<IConfiguration>());

            var response = configurationConnections.GetConnection("ServiceConnection");

            Assert.NotNull(response);
        }

        [Fact]
        public void GetConnection_ShouldThrowOnNullConnectionName()
        {
            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: new Dictionary<string, string>
                    {
                        { "ConnectionsMap:0:ServiceUrl", "*" },
                        { "ConnectionsMap:0:Connection", "ServiceConnection" },
                        { "Connections:ServiceConnection:Type", "MsalAuth" },
                        { "Connections:ServiceConnection:Assembly", "Microsoft.Agents.Authentication.Msal" },
                        { "Connections:ServiceConnection:Settings:ClientId", "ClientId" },
                        { "Connections:ServiceConnection:Settings:ClientSecret", "ClientSecret" },
                        { "Connections:ServiceConnection:Settings:AuthorityEndpoint", "AuthorityEndpoint" },
                    });

            var configurationConnections = new ConfigurationConnections(serviceProvider, serviceProvider.GetService<IConfiguration>());

            Assert.Throws<ArgumentNullException>(() => configurationConnections.GetConnection(null));
        }

        [Fact]
        public void GetDefaultConnection_ShouldReturnAccessTokenProviderFromMap()
        {

            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: new Dictionary<string, string>
                    {
                        { "ConnectionsMap:0:ServiceUrl", "*" },
                        { "ConnectionsMap:0:Connection", "ServiceConnection" },
                        { "Connections:ServiceConnection:Type", "MsalAuth" },
                        { "Connections:ServiceConnection:Assembly", "Microsoft.Agents.Authentication.Msal" },
                        { "Connections:ServiceConnection:Settings:ClientId", "ClientId" },
                        { "Connections:ServiceConnection:Settings:ClientSecret", "ClientSecret" },
                        { "Connections:ServiceConnection:Settings:AuthorityEndpoint", "AuthorityEndpoint" },
                    });

            var configurationConnections = new ConfigurationConnections(serviceProvider, serviceProvider.GetService<IConfiguration>());

            var response = configurationConnections.GetDefaultConnection();

            Assert.NotNull(response);
        }

        [Fact]
        public void GetDefaultConnection_ShouldReturnAccessTokenProviderFromConnections()
        {
            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: new Dictionary<string, string>
                    {
                        { "ConnectionsMap:0:ServiceUrl", "serviceUrl" },
                        { "ConnectionsMap:0:Connection", "ServiceConnection" },
                        { "Connections:ServiceConnection:Type", "MsalAuth" },
                        { "Connections:ServiceConnection:Assembly", "Microsoft.Agents.Authentication.Msal" },
                        { "Connections:ServiceConnection:Settings:ClientId", "ClientId" },
                        { "Connections:ServiceConnection:Settings:ClientSecret", "ClientSecret" },
                        { "Connections:ServiceConnection:Settings:AuthorityEndpoint", "AuthorityEndpoint" },
                    });

            var configurationConnections = new ConfigurationConnections(serviceProvider, serviceProvider.GetService<IConfiguration>());

            var response = configurationConnections.GetDefaultConnection();

            Assert.NotNull(response);
        }

        [Fact]
        public void GetTokenProvider_ShouldReturnAccessTokenProviderOnMatchingServiceUrl()
        {
            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: new Dictionary<string, string>
                    {
                        { "ConnectionsMap:0:ServiceUrl", "serviceUrl" },
                        { "ConnectionsMap:0:Connection", "ServiceConnection" },
                        { "Connections:ServiceConnection:Type", "MsalAuth" },
                        { "Connections:ServiceConnection:Assembly", "Microsoft.Agents.Authentication.Msal" },
                        { "Connections:ServiceConnection:Settings:ClientId", "ClientId" },
                        { "Connections:ServiceConnection:Settings:ClientSecret", "ClientSecret" },
                        { "Connections:ServiceConnection:Settings:AuthorityEndpoint", "AuthorityEndpoint" },
                    });

            var configurationConnections = new ConfigurationConnections(serviceProvider, serviceProvider.GetService<IConfiguration>());

            var response = configurationConnections.GetTokenProvider(_identity, "serviceUrl");

            Assert.NotNull(response);
        }

        [Fact]
        public void GetTokenProvider_ShouldReturnAccessTokenProviderOnEmptyServiceUrl()
        {
            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: new Dictionary<string, string>
                    {
                        { "Connections:ServiceConnection:Type", "MsalAuth" },
                        { "Connections:ServiceConnection:Assembly", "Microsoft.Agents.Authentication.Msal" },
                        { "Connections:ServiceConnection:Settings:ClientId", "ClientId" },
                        { "Connections:ServiceConnection:Settings:ClientSecret", "ClientSecret" },
                        { "Connections:ServiceConnection:Settings:AuthorityEndpoint", "AuthorityEndpoint" },
                    });

            var configurationConnections = new ConfigurationConnections(serviceProvider, serviceProvider.GetService<IConfiguration>());

            var response = configurationConnections.GetTokenProvider(_identity, "serviceUrl");

            Assert.NotNull(response);
        }

        [Fact]
        public void GetTokenProvider_ShouldReturnAccessTokenProviderOnGenericServiceUrl()
        {
            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: new Dictionary<string, string>
                    {
                        { "ConnectionsMap:0:ServiceUrl", "*" },
                        { "ConnectionsMap:0:Connection", "ServiceConnection" },
                        { "Connections:ServiceConnection:Type", "MsalAuth" },
                        { "Connections:ServiceConnection:Assembly", "Microsoft.Agents.Authentication.Msal" },
                        { "Connections:ServiceConnection:Settings:ClientId", "ClientId" },
                        { "Connections:ServiceConnection:Settings:ClientSecret", "ClientSecret" },
                        { "Connections:ServiceConnection:Settings:AuthorityEndpoint", "AuthorityEndpoint" },
                    });

            var configurationConnections = new ConfigurationConnections(serviceProvider, serviceProvider.GetService<IConfiguration>());

            var response = configurationConnections.GetTokenProvider(_identity, "generic");

            Assert.NotNull(response);
        }

        [Fact]
        public void GetTokenProvider_ShouldReturnAccessTokenProviderFromConnectionInstance()
        {
            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: new Dictionary<string, string>
                    {
                        { "ConnectionsMap:0:ServiceUrl", "serviceUrl" },
                        { "ConnectionsMap:0:Connection", "ServiceConnection" },
                        { "Connections:ServiceConnection:Type", "MsalAuth" },
                        { "Connections:ServiceConnection:Assembly", "Microsoft.Agents.Authentication.Msal" },
                        { "Connections:ServiceConnection:Settings:ClientId", "ClientId" },
                        { "Connections:ServiceConnection:Settings:ClientSecret", "ClientSecret" },
                        { "Connections:ServiceConnection:Settings:AuthorityEndpoint", "AuthorityEndpoint" },
                    });

            var configurationConnections = new ConfigurationConnections(serviceProvider, serviceProvider.GetService<IConfiguration>());

            var response = configurationConnections.GetTokenProvider(_identity, "serviceUrl");

            //Call a second time to obtain AccessTokenProvider from the Connection instance
            response = configurationConnections.GetTokenProvider(_identity, "serviceUrl");

            Assert.NotNull(response);
        }

        [Fact]
        public void GetTokenProvider_ShouldReturnNullOnNotMatchingServiceUrl()
        {
            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: new Dictionary<string, string>
                    {
                        { "ConnectionsMap:0:ServiceUrl", "serviceUrl" },
                        { "ConnectionsMap:0:Connection", "ServiceConnection" },
                        { "Connections:ServiceConnection:Type", "MsalAuth" },
                        { "Connections:ServiceConnection:Assembly", "Microsoft.Agents.Authentication.Msal" },
                        { "Connections:ServiceConnection:Settings:ClientId", "ClientId" },
                        { "Connections:ServiceConnection:Settings:ClientSecret", "ClientSecret" },
                        { "Connections:ServiceConnection:Settings:AuthorityEndpoint", "AuthorityEndpoint" },
                    });

            var configurationConnections = new ConfigurationConnections(serviceProvider, serviceProvider.GetService<IConfiguration>());

            var response = configurationConnections.GetTokenProvider(_identity, "noUrl");

            Assert.Null(response);
        }

        [Fact]
        public void GetTokenProvider_ShouldReturnIndexOutOfRangeExceptionOnEmptyConnections()
        {
            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: new Dictionary<string, string>
                    {
                        { "ConnectionsMap:0:ServiceUrl", "*" },
                        { "ConnectionsMap:0:Connection", "ServiceConnection" },
                        { "ConnectionsMap:0:Audience", "audience" }
                    });

            var configurationConnections = new ConfigurationConnections(serviceProvider, serviceProvider.GetService<IConfiguration>());

            var claims = new List<Claim>
            {
                new(AuthenticationConstants.AudienceClaim, "audience"),
            };
            ClaimsIdentity identity = new(claims);

            try
            {
                var response = configurationConnections.GetTokenProvider(identity, "serviceUrl");
            }
            catch (IndexOutOfRangeException e)
            {
                ExceptionTester.IsException<IndexOutOfRangeException>(e, ErrorHelper.ConnectionNotFoundByName.code, _outputListener);
                return; 
            }
            throw new Exception("Should not reach this point");
        }

        [Fact]
        public void GetProviderConstructor_ShouldReturnConstructorInfoOnValidProviderType()
        {
            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: null);
#if !NETFRAMEWORK
            var assemblyLoader = new AuthModuleLoader(AssemblyLoadContext.Default, serviceProvider.GetService<ILogger<ConfigurationConnections>>());
#else
            var assemblyLoader = new AuthModuleLoader(AppDomain.CurrentDomain, serviceProvider.GetService<ILogger<ConfigurationConnections>>());
#endif

            var response = assemblyLoader.GetProviderConstructor("name", "Microsoft.Agents.Authentication.Msal", "MsalAuth");

            Assert.NotNull(response);
        }

        [Fact]
        public void GetProviderConstructors_ShouldReturnConstructorsForValidAssembly()
        {
            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: null);
#if !NETFRAMEWORK
            var assemblyLoader = new AuthModuleLoader(AssemblyLoadContext.Default, serviceProvider.GetService<ILogger<ConfigurationConnections>>());
#else
            var assemblyLoader = new AuthModuleLoader(AppDomain.CurrentDomain, serviceProvider.GetService<ILogger<ConfigurationConnections>>());
#endif

            var constructors = assemblyLoader.GetProviderConstructors("Microsoft.Agents.Authentication.Msal");

            Assert.NotNull(constructors);
            var list = new List<System.Reflection.ConstructorInfo>(constructors);
            Assert.True(list.Count > 0);
        }

        [Fact]
        public void GetProviderConstructors_ShouldThrowOnNullAssemblyName()
        {
            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: null);
#if !NETFRAMEWORK
            var assemblyLoader = new AuthModuleLoader(AssemblyLoadContext.Default, serviceProvider.GetService<ILogger<ConfigurationConnections>>());
#else
            var assemblyLoader = new AuthModuleLoader(AppDomain.CurrentDomain, serviceProvider.GetService<ILogger<ConfigurationConnections>>());
#endif

            Assert.Throws<ArgumentNullException>(() =>
            {
                foreach (var _ in assemblyLoader.GetProviderConstructors(null)) { }
            });
        }

        [Fact]
        public void GetProviderConstructor_ShouldReturnConstructorInfoOnNullType()
        {
            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: null);
#if !NETFRAMEWORK
            var assemblyLoader = new AuthModuleLoader(AssemblyLoadContext.Default, serviceProvider.GetService<ILogger<ConfigurationConnections>>());
#else
            var assemblyLoader = new AuthModuleLoader(AppDomain.CurrentDomain, serviceProvider.GetService<ILogger<ConfigurationConnections>>());
#endif

            var response = assemblyLoader.GetProviderConstructor("name", "Microsoft.Agents.Authentication.Msal", null);

            Assert.NotNull(response);
        }

        [Fact]
        public void GetProviderConstructor_ShouldThrowOnNullLoadContext()
        {
            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: null);
            Assert.Throws<ArgumentNullException>(() => new AuthModuleLoader(null, serviceProvider.GetService<ILogger<ConfigurationConnections>>()));
        }

        [Fact]
        public void GetProviderConstructor_ShouldThrowOnNullAssemblyName()
        {
            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: null);
#if !NETFRAMEWORK
            var assemblyLoader = new AuthModuleLoader(AssemblyLoadContext.Default, serviceProvider.GetService<ILogger<ConfigurationConnections>>());
#else
            var assemblyLoader = new AuthModuleLoader(AppDomain.CurrentDomain, serviceProvider.GetService<ILogger<ConfigurationConnections>>());
#endif

            try
            {
                assemblyLoader.GetProviderConstructor("name", null, "type-name");
            }
            catch(InvalidOperationException ex)
            {
                ExceptionTester.IsException<InvalidOperationException>(ex, ErrorHelper.AuthProviderTypeNotFound.code, _outputListener);
                return;
            }
            throw new Exception("Should not reach this point");
        }

        [Fact]
        public void GetProviderConstructor_ShouldThrowOnInvalidProviderType()
        {
            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: null);
#if !NETFRAMEWORK
            var assemblyLoader = new AuthModuleLoader(AssemblyLoadContext.Default, serviceProvider.GetService<ILogger<ConfigurationConnections>>());
#else
            var assemblyLoader = new AuthModuleLoader(AppDomain.CurrentDomain, serviceProvider.GetService<ILogger<ConfigurationConnections>>());
#endif
            try
            {
                assemblyLoader.GetProviderConstructor("name", "Microsoft.Agents.Authentication.Msal", "type");
            }
            catch (InvalidOperationException ex)
            {
                ExceptionTester.IsException<InvalidOperationException>(ex, ErrorHelper.AuthProviderTypeNotFound.code, _outputListener);
                return;
            }
            throw new Exception("Should not reach this point");
        }

        [Fact]
        public void ManualConstruction()
        {
            var sp = new Mock<IServiceProvider>();
            var providers = new Dictionary<string, IAccessTokenProvider>
            {
                {
                    "ServiceConnection", new MsalAuth(sp.Object, new ConnectionSettings()
                    {
                        ClientId = "12345",
                        AuthType = AuthTypes.ClientSecret,
                    }
                )}
            };

            List<ConnectionMapItem> mapItems = new List<ConnectionMapItem> { new ConnectionMapItem { ServiceUrl = "*", Connection = "ServiceConnection" } };
            var connection = new ConfigurationConnections(providers, mapItems);
            Assert.True(connection.TryGetConnection("ServiceConnection", out var provider));
            Assert.NotNull(provider);
        }

        [Fact]
        public void TryGetConnection_ReturnsFalseForNullName()
        {
            var sp = new Mock<IServiceProvider>();
            var providers = new Dictionary<string, IAccessTokenProvider>
            {
                {
                    "ServiceConnection", new MsalAuth(sp.Object, new ConnectionSettings()
                    {
                        ClientId = "12345",
                        AuthType = AuthTypes.ClientSecret,
                    })
                }
            };
            var mapItems = new List<ConnectionMapItem> { new ConnectionMapItem { ServiceUrl = "*", Connection = "ServiceConnection" } };
            var connection = new ConfigurationConnections(providers, mapItems);

            Assert.False(connection.TryGetConnection(null, out var provider));
            Assert.Null(provider);
        }

        [Fact]
        public void TryGetConnection_ReturnsFalseForNonExistentName()
        {
            var sp = new Mock<IServiceProvider>();
            var providers = new Dictionary<string, IAccessTokenProvider>
            {
                {
                    "ServiceConnection", new MsalAuth(sp.Object, new ConnectionSettings()
                    {
                        ClientId = "12345",
                        AuthType = AuthTypes.ClientSecret,
                    })
                }
            };
            var mapItems = new List<ConnectionMapItem> { new ConnectionMapItem { ServiceUrl = "*", Connection = "ServiceConnection" } };
            var connection = new ConfigurationConnections(providers, mapItems);

            Assert.False(connection.TryGetConnection("NonExistent", out var provider));
            Assert.Null(provider);
        }

        [Fact]
        public void GetTokenProvider_WithActivity_ReturnsProviderForNonAgenticRole()
        {
            var primaryProvider = new Mock<IAccessTokenProvider>();
            primaryProvider.Setup(p => p.ConnectionSettings).Returns((ImmutableConnectionSettings)null);

            var providers = new Dictionary<string, IAccessTokenProvider>
            {
                { "ServiceConnection", primaryProvider.Object },
            };
            var mapItems = new List<ConnectionMapItem> { new ConnectionMapItem { ServiceUrl = "*", Connection = "ServiceConnection" } };
            var connections = new ConfigurationConnections(providers, mapItems);

            var activity = new Mock<IActivity>();
            activity.Setup(a => a.ServiceUrl).Returns("serviceUrl");
            activity.Setup(a => a.Recipient).Returns(new ChannelAccount { Role = "user" });

            var response = connections.GetTokenProvider(_identity, activity.Object);

            Assert.Same(primaryProvider.Object, response);
        }

        [Fact]
        public void GetTokenProvider_WithActivity_UsesAlternateBlueprintForAgenticIdentityRole()
        {
            var primaryProvider = new Mock<IAccessTokenProvider>();
            primaryProvider.Setup(p => p.ConnectionSettings).Returns(new ImmutableConnectionSettings(
                new TestSettingsWithAlternate("AlternateConnection")));

            var alternateProvider = new Mock<IAccessTokenProvider>();
            alternateProvider.Setup(p => p.ConnectionSettings).Returns((ImmutableConnectionSettings)null);

            var providers = new Dictionary<string, IAccessTokenProvider>
            {
                { "ServiceConnection", primaryProvider.Object },
                { "AlternateConnection", alternateProvider.Object }
            };
            var mapItems = new List<ConnectionMapItem> { new ConnectionMapItem { ServiceUrl = "*", Connection = "ServiceConnection" } };
            var connections = new ConfigurationConnections(providers, mapItems);

            var activity = new Mock<IActivity>();
            activity.Setup(a => a.ServiceUrl).Returns("serviceUrl");
            activity.Setup(a => a.Recipient).Returns(new ChannelAccount { Role = RoleTypes.AgenticIdentity });

            var response = connections.GetTokenProvider(_identity, activity.Object);

            Assert.Same(alternateProvider.Object, response);
        }

        [Fact]
        public void GetTokenProvider_WithActivity_UsesAlternateBlueprintForAgenticUserRole()
        {
            var primaryProvider = new Mock<IAccessTokenProvider>();
            primaryProvider.Setup(p => p.ConnectionSettings).Returns(new ImmutableConnectionSettings(
                new TestSettingsWithAlternate("AlternateConnection")));

            var alternateProvider = new Mock<IAccessTokenProvider>();
            alternateProvider.Setup(p => p.ConnectionSettings).Returns((ImmutableConnectionSettings)null);

            var providers = new Dictionary<string, IAccessTokenProvider>
            {
                { "ServiceConnection", primaryProvider.Object },
                { "AlternateConnection", alternateProvider.Object }
            };
            var mapItems = new List<ConnectionMapItem> { new ConnectionMapItem { ServiceUrl = "*", Connection = "ServiceConnection" } };
            var connections = new ConfigurationConnections(providers, mapItems);

            var activity = new Mock<IActivity>();
            activity.Setup(a => a.ServiceUrl).Returns("serviceUrl");
            activity.Setup(a => a.Recipient).Returns(new ChannelAccount { Role = RoleTypes.AgenticUser });

            var response = connections.GetTokenProvider(_identity, activity.Object);

            Assert.Same(alternateProvider.Object, response);
        }

        [Fact]
        public void GetTokenProvider_WithActivity_DoesNotUseAlternateWhenBlank()
        {
            var primaryProvider = new Mock<IAccessTokenProvider>();
            primaryProvider.Setup(p => p.ConnectionSettings).Returns(new ImmutableConnectionSettings(
                new TestSettingsWithAlternate(null)));

            var providers = new Dictionary<string, IAccessTokenProvider>
            {
                { "ServiceConnection", primaryProvider.Object },
            };
            var mapItems = new List<ConnectionMapItem> { new ConnectionMapItem { ServiceUrl = "*", Connection = "ServiceConnection" } };
            var connections = new ConfigurationConnections(providers, mapItems);

            var activity = new Mock<IActivity>();
            activity.Setup(a => a.ServiceUrl).Returns("serviceUrl");
            activity.Setup(a => a.Recipient).Returns(new ChannelAccount { Role = RoleTypes.AgenticIdentity });

            var response = connections.GetTokenProvider(_identity, activity.Object);

            Assert.Same(primaryProvider.Object, response);
        }
    }

    internal class TestSettingsWithAlternate : ConnectionSettingsBase
    {
        public TestSettingsWithAlternate(string alternateBlueprintConnectionName)
        {
            AlternateBlueprintConnectionName = alternateBlueprintConnectionName;
        }
    }
}
