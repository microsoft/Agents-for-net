// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication.Msal.Model;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Authentication.Msal.Tests
{
    public class MsalAuthTests
    {
        private static readonly Mock<IServiceProvider> _service = new Mock<IServiceProvider>();

        private static readonly Dictionary<string, string> _configSettings = new()
        {
            { "Connections:ServiceConnection:Settings:AuthType", "ClientSecret" },
            { "Connections:ServiceConnection:Settings:ClientId", "test-id" },
            { "Connections:ServiceConnection:Settings:ClientSecret", "test-secret" },
            { "Connections:ServiceConnection:Settings:TenantId", "test-tenant" },
        };
        private const string SettingsSection = "Connections:ServiceConnection:Settings";

        IConfiguration _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(_configSettings)
            .Build();

        private const string ResourceUrl = "https://example.com";
        private readonly List<string>  _scopes = ["scope1"];

        [Fact]
        public void Constructor_ShouldInstantiateCorrectly()
        {
            var msal = new MsalAuth(_service.Object, _configuration.GetSection(SettingsSection));

            Assert.NotNull(msal);
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullServiceProvider()
        {
            Assert.Throws<ArgumentNullException>(() => new MsalAuth(null, _configuration.GetSection(SettingsSection)));
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullConfiguration()
        {
            Assert.Throws<ArgumentNullException>(() => new MsalAuth(_service.Object, (IConfigurationSection) null));
        }

        [Fact]
        public async Task GetAccessTokenAsync_ShouldThrowOnMalformedUri()
        {
            var msalAuth = new MsalAuth(_service.Object, _configuration.GetSection(SettingsSection));

            await Assert.ThrowsAsync<ArgumentException>(() => msalAuth.GetAccessTokenAsync(null, _scopes, false));
        }

        [Fact]
        public async Task GetAccessTokenAsync_ShouldThrowOnNullScopesForClientCredentials()
        {
            var options = new Mock<IOptions<MsalAuthConfigurationOptions>>();

            var returnedOptions = new MsalAuthConfigurationOptions
            {
                MSALEnabledLogPII = false
            };
            options.Setup(x => x.Value).Returns(returnedOptions).Verifiable(Times.Exactly(2));

            var logger = new Mock<ILogger<MsalAuth>>();

            var service = new Mock<IServiceProvider>();
            service.Setup(x => x.GetService(typeof(IOptions<MsalAuthConfigurationOptions>)))
                .Returns(options.Object)
                .Verifiable(Times.Exactly(2));
            service.Setup(x => x.GetService(typeof(ILogger<MsalAuth>)))
                .Returns(logger.Object)
                .Verifiable(Times.Once);

            var msalAuth = new MsalAuth(service.Object, _configuration.GetSection(SettingsSection));

            await Assert.ThrowsAsync<ArgumentException>(() => msalAuth.GetAccessTokenAsync(ResourceUrl, null, false));
            Mock.Verify(options, service);
        }

        [Fact]
        public async Task GetAccessTokenAsync_ShouldReturnTokenFromCache()
        {
            var token = "valid_token";
            var authResult = new AuthenticationResult(token, false, null, DateTimeOffset.UtcNow.AddMinutes(5), DateTimeOffset.UtcNow.AddMinutes(5), null, null, null, null, Guid.NewGuid());
            var authResults = new ExecuteAuthenticationResults { MsalAuthResult = authResult };
            var cacheList = new ConcurrentDictionary<Uri, ExecuteAuthenticationResults>();
            cacheList.TryAdd(new Uri(ResourceUrl), authResults);

            var msalAuth = new MsalAuth(_service.Object, _configuration.GetSection(SettingsSection));

            // Use reflection to set the private _cacheList property
            var cacheListField = typeof(MsalAuth).GetField("_cacheList", BindingFlags.NonPublic | BindingFlags.Instance);
            cacheListField.SetValue(msalAuth, cacheList);

            var result = await msalAuth.GetAccessTokenAsync(ResourceUrl, _scopes);

            Assert.Equal(token, result);
        }

        [Fact]
        public async Task GetAccessTokenAsync_ShouldRefreshTokenWhenExpired()
        {
            var expiredToken = "expired_token";
            var newToken = "token";
            var expiredAuthResult = new AuthenticationResult(expiredToken, false, null, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(-5), null, null, null, null, Guid.NewGuid());
            var newAuthResult = new AuthenticationResult(newToken, false, null, DateTimeOffset.UtcNow.AddMinutes(5), DateTimeOffset.UtcNow.AddMinutes(5), null, null, null, null, Guid.NewGuid());
            var expiredAuthResults = new ExecuteAuthenticationResults { MsalAuthResult = expiredAuthResult };
            var cacheList = new ConcurrentDictionary<Uri, ExecuteAuthenticationResults>();
            cacheList.TryAdd(new Uri(ResourceUrl), expiredAuthResults);

            var options = new Mock<IOptions<MsalAuthConfigurationOptions>>();

            var returnedOptions = new MsalAuthConfigurationOptions
            {
                MSALEnabledLogPII = false
            };
            options.Setup(x => x.Value).Returns(returnedOptions).Verifiable(Times.Exactly(2));

            var logger = new Mock<ILogger<MsalAuth>>();

            var service = new Mock<IServiceProvider>();
            service.Setup(x => x.GetService(typeof(IOptions<MsalAuthConfigurationOptions>)))
                .Returns(options.Object)
                .Verifiable(Times.Exactly(2));
            service.Setup(x => x.GetService(typeof(ILogger<MsalAuth>)))
                .Returns(logger.Object)
                .Verifiable(Times.Once);
            service.Setup(sp => sp.GetService(typeof(IHttpClientFactory)))
                .Returns(new TestHttpClientFactory())
                .Verifiable(Times.AtLeast(1));

            var msalAuth = new MsalAuth(service.Object, _configuration.GetSection(SettingsSection));
            
            // Use reflection to set the private _cacheList property
            var cacheListField = typeof(MsalAuth).GetField("_cacheList", BindingFlags.NonPublic | BindingFlags.Instance);
            cacheListField.SetValue(msalAuth, cacheList);

            var result = await msalAuth.GetAccessTokenAsync(ResourceUrl, _scopes);

            Assert.Equal(newToken, result);
            Mock.Verify(options, service);
        }

        [Fact]
        public async Task GetAccessTokenAsync_ShouldReturnTokenForClientCredentials()
        {
            var token = "token";

            var options = new Mock<IOptions<MsalAuthConfigurationOptions>>();

            var returnedOptions = new MsalAuthConfigurationOptions
            {
                MSALEnabledLogPII = false
            };
            options.Setup(x => x.Value).Returns(returnedOptions).Verifiable(Times.Exactly(2));

            var logger = new Mock<ILogger<MsalAuth>>();

            var service = new Mock<IServiceProvider>();
            service.Setup(x => x.GetService(typeof(IOptions<MsalAuthConfigurationOptions>)))
                .Returns(options.Object)
                .Verifiable(Times.Exactly(2));
            service.Setup(x => x.GetService(typeof(ILogger<MsalAuth>)))
                .Returns(logger.Object)
                .Verifiable(Times.Once);
            service.Setup(sp => sp.GetService(typeof(IHttpClientFactory)))
                .Returns(new TestHttpClientFactory())
                .Verifiable(Times.Once);

            var msalAuth = new MsalAuth(service.Object, _configuration.GetSection(SettingsSection));

            var result = await msalAuth.GetAccessTokenAsync(ResourceUrl, _scopes, true);

            Assert.Equal(token, result);
            Mock.Verify(options, service);
        }

        [Fact]
        public async Task GetAccessTokenAsync_ShouldReturnTokenForCertificate()
        {
            var token = "token";

            var configSettings = new Dictionary<string, string> {
                { "Connections:ServiceConnection:Settings:AuthType", "Certificate" },
                { "Connections:ServiceConnection:Settings:ClientId", "test-id" },
                { "Connections:ServiceConnection:Settings:AuthorityEndpoint", "https://botframework/test.com" },
                { "Connections:ServiceConnection:Settings:CertThumbprint", "thumbprint" },
                { "Connections:ServiceConnection:Settings:Scopes:scope1", "{instance}" }
            };

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();

            var options = new Mock<IOptions<MsalAuthConfigurationOptions>>();

            var returnedOptions = new MsalAuthConfigurationOptions
            {
                MSALEnabledLogPII = false
            };
            options.Setup(x => x.Value).Returns(returnedOptions).Verifiable(Times.Exactly(2));

            var logger = new Mock<ILogger<MsalAuth>>();

            var certificate = new Mock<ICertificateProvider>();
            certificate.Setup(x => x.GetCertificate())
                .Returns(CreateSelfSignedCertificate("test"))
                .Verifiable(Times.Once);

            var service = new Mock<IServiceProvider>();
            service.Setup(x => x.GetService(typeof(IOptions<MsalAuthConfigurationOptions>)))
                .Returns(options.Object)
                .Verifiable(Times.Exactly(2));
            service.Setup(x => x.GetService(typeof(ILogger<MsalAuth>)))
                .Returns(logger.Object)
                .Verifiable(Times.Once);
            service.Setup(sp => sp.GetService(typeof(IHttpClientFactory)))
                .Returns(new TestHttpClientFactory())
                .Verifiable(Times.Exactly(2));
            service.Setup(sp => sp.GetService(typeof(ICertificateProvider)))
                .Returns(certificate.Object)
                .Verifiable(Times.Once);

            var msalAuth = new MsalAuth(service.Object, configuration.GetSection(SettingsSection));

            var result = await msalAuth.GetAccessTokenAsync(ResourceUrl, [], true);

            Assert.Equal(token, result);
            Mock.Verify(options, service, certificate);
        }

        [Fact]
        public void MSALProvider_ClientSecretShouldReturnConfidentialClient()
        {
            Dictionary<string, string> configSettings = new Dictionary<string, string> {
                { "Connections:ServiceConnection:Settings:AuthType", "ClientSecret" },
                { "Connections:ServiceConnection:Settings:ClientId", "test-id" },
                { "Connections:ServiceConnection:Settings:ClientSecret", "test-secret" },
                { "Connections:ServiceConnection:Settings:TenantId", "test-tenant" },
            };
            string settingsSection = "Connections:ServiceConnection:Settings";

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();

            var options = new Mock<IOptions<MsalAuthConfigurationOptions>>();

            var returnedOptions = new MsalAuthConfigurationOptions
            {
                MSALEnabledLogPII = false
            };
            options.Setup(x => x.Value).Returns(returnedOptions).Verifiable(Times.Exactly(2));

            var logger = new Mock<ILogger<MsalAuth>>();

            var service = new Mock<IServiceProvider>();
            service.Setup(x => x.GetService(typeof(IOptions<MsalAuthConfigurationOptions>)))
                .Returns(options.Object)
                .Verifiable(Times.Exactly(2));
            service.Setup(x => x.GetService(typeof(ILogger<MsalAuth>)))
                .Returns(logger.Object)
                .Verifiable(Times.Once);

            var msal = new MsalAuth(service.Object, configuration.GetSection(settingsSection));

            var msalProvider = msal as IMSALProvider;
            Assert.NotNull(msalProvider);
            Assert.IsAssignableFrom<IConfidentialClientApplication>(msalProvider.CreateClientApplication());
        }

        [Fact]
        public void MSALProvider_UserManagedIdentityShouldReturnManagedIdentityApplication()
        {
            Dictionary<string, string> configSettings = new Dictionary<string, string> {
                { "Connections:ServiceConnection:Settings:AuthType", "UserManagedIdentity" },
                { "Connections:ServiceConnection:Settings:ClientId", "test-id" },
                { "Connections:ServiceConnection:Settings:TenantId", "test-tenant" },
            };
            string settingsSection = "Connections:ServiceConnection:Settings";

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();

            var options = new Mock<IOptions<MsalAuthConfigurationOptions>>();

            var returnedOptions = new MsalAuthConfigurationOptions
            {
                MSALEnabledLogPII = false
            };
            options.Setup(x => x.Value).Returns(returnedOptions).Verifiable(Times.Exactly(2));

            var logger = new Mock<ILogger<MsalAuth>>();

            var service = new Mock<IServiceProvider>();
            service.Setup(x => x.GetService(typeof(IOptions<MsalAuthConfigurationOptions>)))
                .Returns(options.Object)
                .Verifiable(Times.Exactly(2));
            service.Setup(x => x.GetService(typeof(ILogger<MsalAuth>)))
                .Returns(logger.Object)
                .Verifiable(Times.Once);

            var msal = new MsalAuth(service.Object, configuration.GetSection(settingsSection));

            var msalProvider = msal as IMSALProvider;
            Assert.NotNull(msalProvider);
            Assert.IsAssignableFrom<IManagedIdentityApplication>(msalProvider.CreateClientApplication());
        }

        [Fact]
        public async Task MSALProvider_Agentic_CommonReplacement()
        {
            Dictionary<string, string> configSettings = new Dictionary<string, string> {
                { "Connections:ServiceConnection:Settings:AuthType", "ClientSecret" },
                { "Connections:ServiceConnection:Settings:ClientId", "test-id-1" },
                { "Connections:ServiceConnection:Settings:ClientSecret", "test-secret" },
                { "Connections:ServiceConnection:Settings:AuthorityEndpoint", "https://login.microsoftonline.com/common" },
            };
            string settingsSection = "Connections:ServiceConnection:Settings";

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();

            var options = new Mock<IOptions<MsalAuthConfigurationOptions>>();

            var returnedOptions = new MsalAuthConfigurationOptions
            {
                MSALEnabledLogPII = false
            };
            options.Setup(x => x.Value).Returns(returnedOptions);

            var logger = new Mock<ILogger<MsalAuth>>();

            var service = new Mock<IServiceProvider>();
            service.Setup(x => x.GetService(typeof(IOptions<MsalAuthConfigurationOptions>)))
                .Returns(options.Object);
            service.Setup(x => x.GetService(typeof(ILogger<MsalAuth>)))
                .Returns(logger.Object)
                .Verifiable(Times.Once);
            service.Setup(sp => sp.GetService(typeof(IHttpClientFactory)))
                .Returns(new TestHttpClientFactory((httpRequest) =>
                {
                    string uri;
                    if (httpRequest.RequestUri.ToString().Contains("/common/discovery"))
                    {
                        uri = httpRequest.RequestUri.Query;
                    }
                    else
                    {
                        uri = httpRequest.RequestUri.ToString();
                    }

                    Assert.DoesNotContain("common", uri);
                    Assert.Contains("new-tenant", uri);
                }));

            var msal = new MsalAuth(service.Object, configuration.GetSection(settingsSection));
            await msal.GetAgenticUserTokenAsync("new-tenant", "aai", "upn", ["scope-1"]);
            Mock.Verify(options, service);
        }

        [Fact]
        public async Task MSALProvider_Agentic_GuidTenantIdReplacement()
        {
            const string guidTenantId = "12345678-1234-1234-1234-123456789abc";
            
            Dictionary<string, string> configSettings = new Dictionary<string, string> {
                { "Connections:ServiceConnection:Settings:AuthType", "ClientSecret" },
                { "Connections:ServiceConnection:Settings:ClientId", "test-id-guid" },
                { "Connections:ServiceConnection:Settings:ClientSecret", "test-secret" },
                { "Connections:ServiceConnection:Settings:AuthorityEndpoint", $"https://login.microsoftonline.com/{guidTenantId}" },
            };
            string settingsSection = "Connections:ServiceConnection:Settings";

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();

            var options = new Mock<IOptions<MsalAuthConfigurationOptions>>();

            var returnedOptions = new MsalAuthConfigurationOptions
            {
                MSALEnabledLogPII = false
            };
            options.Setup(x => x.Value).Returns(returnedOptions).Verifiable(Times.Exactly(6));

            var logger = new Mock<ILogger<MsalAuth>>();

            var service = new Mock<IServiceProvider>();
            service.Setup(x => x.GetService(typeof(IOptions<MsalAuthConfigurationOptions>)))
                .Returns(options.Object)
                .Verifiable(Times.Exactly(6));
            service.Setup(x => x.GetService(typeof(ILogger<MsalAuth>)))
                .Returns(logger.Object)
                .Verifiable(Times.Once);
            service.Setup(sp => sp.GetService(typeof(IHttpClientFactory)))
                .Returns(new TestHttpClientFactory((httpRequest) =>
                {
                    string uri;
                    if (httpRequest.RequestUri.ToString().Contains($"/{guidTenantId}/discovery"))
                    {
                        uri = httpRequest.RequestUri.Query;
                    }
                    else
                    {
                        uri = httpRequest.RequestUri.ToString();
                    }

                    Assert.DoesNotContain(guidTenantId, uri);
                    Assert.Contains("new-tenant", uri);
                }))
                .Verifiable(Times.Exactly(5));

            var msal = new MsalAuth(service.Object, configuration.GetSection(settingsSection));
            await msal.GetAgenticUserTokenAsync("new-tenant", "aai-guid", "upn-guid", ["scope-guid"]);
            Mock.Verify(options, service);
        }

        [Fact]
        public async Task MSALProvider_Agentic_CommonReplacementSkipped()
        {
            Dictionary<string, string> configSettings = new Dictionary<string, string> {
                { "Connections:ServiceConnection:Settings:AuthType", "ClientSecret" },
                { "Connections:ServiceConnection:Settings:ClientId", "test-id-2" },
                { "Connections:ServiceConnection:Settings:ClientSecret", "test-secret" },
                { "Connections:ServiceConnection:Settings:AuthorityEndpoint", "https://login.microsoftonline.com/common" },
            };
            string settingsSection = "Connections:ServiceConnection:Settings";

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();

            var options = new Mock<IOptions<MsalAuthConfigurationOptions>>();

            var returnedOptions = new MsalAuthConfigurationOptions
            {
                MSALEnabledLogPII = false
            };
            options.Setup(x => x.Value).Returns(returnedOptions).Verifiable(Times.Exactly(6));

            var logger = new Mock<ILogger<MsalAuth>>();

            var service = new Mock<IServiceProvider>();
            service.Setup(x => x.GetService(typeof(IOptions<MsalAuthConfigurationOptions>)))
                .Returns(options.Object)
                .Verifiable(Times.Exactly(6));
            service.Setup(x => x.GetService(typeof(ILogger<MsalAuth>)))
                .Returns(logger.Object)
                .Verifiable(Times.Once);
            service.Setup(sp => sp.GetService(typeof(IHttpClientFactory)))
                .Returns(new TestHttpClientFactory((httpRequest) =>
                {
                    string uri;
                    if (httpRequest.RequestUri.ToString().Contains("/common/discovery"))
                    {
                        uri = httpRequest.RequestUri.Query;
                    }
                    else
                    {
                        uri = httpRequest.RequestUri.ToString();
                    }

                    Assert.Contains("common", uri);
                }))
                .Verifiable(Times.AtLeast(4));

            var msal = new MsalAuth(service.Object, configuration.GetSection(settingsSection));

            // if tenantId not specified, authority is unchanged.
            await msal.GetAgenticUserTokenAsync(null, "aai-2", "upn-2", ["scope-2"]);

            Mock.Verify(options, service);
        }

        [Fact]
        public async Task MSALProvider_Agentic_InstanceToken_UsesProvidedScopes()
        {
            const string customScope = "api://custom-resource/.default";
            const string agentAppInstanceId = "aai-custom";
            var instanceTokenRequestScopeTcs = new TaskCompletionSource<string>();
            const string settingsSection = "Connections:ServiceConnection:Settings";
            Dictionary<string, string> configSettings = new()
            {
                { "Connections:ServiceConnection:Settings:AuthType", "ClientSecret" },
                { "Connections:ServiceConnection:Settings:ClientId", "test-id-custom" },
                { "Connections:ServiceConnection:Settings:ClientSecret", "test-secret-custom" },
                { "Connections:ServiceConnection:Settings:AuthorityEndpoint", "https://custom-authority.example.com/common" },
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();

            var options = new Mock<IOptions<MsalAuthConfigurationOptions>>();
            options.Setup(x => x.Value).Returns(new MsalAuthConfigurationOptions { MSALEnabledLogPII = false });

            var logger = new Mock<ILogger<MsalAuth>>();

            var service = new Mock<IServiceProvider>();
            service.Setup(x => x.GetService(typeof(IOptions<MsalAuthConfigurationOptions>))).Returns(options.Object);
            service.Setup(x => x.GetService(typeof(ILogger<MsalAuth>))).Returns(logger.Object);
            service.Setup(sp => sp.GetService(typeof(IHttpClientFactory)))
                .Returns(new TestHttpClientFactory(async (httpRequest) =>
                {
                    if (httpRequest.RequestUri.ToString().Contains("/oauth2/v2.0/token"))
                    {
                        var formValues = await ParseFormBodyAsync(httpRequest);
                        if (formValues.TryGetValue("client_id", out var clientId) && clientId == agentAppInstanceId)
                        {
                            formValues.TryGetValue("scope", out var scope);
                            instanceTokenRequestScopeTcs.TrySetResult(scope);
                        }
                    }
                }));

            var msal = new MsalAuth(service.Object, configuration.GetSection(settingsSection));
            await msal.GetAgenticInstanceTokenAsync("tenant-custom", agentAppInstanceId, [customScope]);
            var completedTask = await Task.WhenAny(instanceTokenRequestScopeTcs.Task, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.Same(instanceTokenRequestScopeTcs.Task, completedTask);
            var instanceTokenRequestScope = await instanceTokenRequestScopeTcs.Task;
            Assert.Equal(customScope, instanceTokenRequestScope);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task MSALProvider_Agentic_InstanceToken_UsesDefaultScopeWhenScopesAreNullOrEmpty(bool useNullScopes)
        {
            const string defaultScope = "api://AzureAdTokenExchange/.default";
            const string agentAppInstanceId = "aai-default";
            var instanceTokenRequestScopeTcs = new TaskCompletionSource<string>();
            const string settingsSection = "Connections:ServiceConnection:Settings";
            Dictionary<string, string> configSettings = new()
            {
                { "Connections:ServiceConnection:Settings:AuthType", "ClientSecret" },
                { "Connections:ServiceConnection:Settings:ClientId", "test-id-default" },
                { "Connections:ServiceConnection:Settings:ClientSecret", "test-secret-default" },
                { "Connections:ServiceConnection:Settings:AuthorityEndpoint", "https://default-authority.example.com/common" },
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();

            var options = new Mock<IOptions<MsalAuthConfigurationOptions>>();
            options.Setup(x => x.Value).Returns(new MsalAuthConfigurationOptions { MSALEnabledLogPII = false });

            var logger = new Mock<ILogger<MsalAuth>>();

            var service = new Mock<IServiceProvider>();
            service.Setup(x => x.GetService(typeof(IOptions<MsalAuthConfigurationOptions>))).Returns(options.Object);
            service.Setup(x => x.GetService(typeof(ILogger<MsalAuth>))).Returns(logger.Object);
            service.Setup(sp => sp.GetService(typeof(IHttpClientFactory)))
                .Returns(new TestHttpClientFactory(async (httpRequest) =>
                {
                    if (httpRequest.RequestUri.ToString().Contains("/oauth2/v2.0/token"))
                    {
                        var formValues = await ParseFormBodyAsync(httpRequest);
                        if (formValues.TryGetValue("client_id", out var clientId) && clientId == agentAppInstanceId)
                        {
                            formValues.TryGetValue("scope", out var scope);
                            instanceTokenRequestScopeTcs.TrySetResult(scope);
                        }
                    }
                }));

            IList<string> scopes = useNullScopes ? null : [];
            var msal = new MsalAuth(service.Object, configuration.GetSection(settingsSection));
            await msal.GetAgenticInstanceTokenAsync("tenant-default", agentAppInstanceId, scopes);
            var completedTask = await Task.WhenAny(instanceTokenRequestScopeTcs.Task, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.Same(instanceTokenRequestScopeTcs.Task, completedTask);
            var instanceTokenRequestScope = await instanceTokenRequestScopeTcs.Task;
            Assert.Equal(defaultScope, instanceTokenRequestScope);
        }

        private static X509Certificate2 CreateSelfSignedCertificate(string subjectName)
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest($"CN={subjectName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
            request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

            var certificate = request.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(1));

            return certificate;
        }

        private static async Task<Dictionary<string, string>> ParseFormBodyAsync(HttpRequestMessage request)
        {
            var requestBody = await request.Content.ReadAsStringAsync();
            var formValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var keyValuePair in requestBody.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var valueParts = keyValuePair.Split('=', 2);
                var key = Uri.UnescapeDataString(valueParts[0].Replace("+", " "));
                var value = valueParts.Length > 1 ? Uri.UnescapeDataString(valueParts[1].Replace("+", " ")) : string.Empty;
                formValues[key] = value;
            }

            return formValues;
        }

        private class TestHttpClientFactory : IHttpClientFactory
        {
            private readonly Func<HttpRequestMessage, Task> _assertFunc;

            public TestHttpClientFactory(Action<HttpRequestMessage> assertFunc = null)
            {
                _assertFunc = assertFunc != null
                    ? (httpRequest) =>
                    {
                        assertFunc(httpRequest);
                        return Task.CompletedTask;
                    }
                    : null;
            }

            public TestHttpClientFactory(Func<HttpRequestMessage, Task> assertFunc)
            {
                _assertFunc = assertFunc;
            }

            public HttpClient CreateClient(string name)
            {
                var response = new
                {
                    Access_token = "token",
                    Token_type = "Bearer",
                    Expires_in = 3600,
                    Scope = "create"
                };
                var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(ProtocolJsonSerializer.ToJson(response))
                };

                var client = new Mock<HttpClient>();
                client
                    .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                    .Returns<HttpRequestMessage, CancellationToken>(async (httpRequest, ct) =>
                    {
                        if (_assertFunc != null)
                        {
                            await _assertFunc(httpRequest);
                        }

                        return httpResponse;
                    });
                return client.Object;
            }
        }
    }
}
