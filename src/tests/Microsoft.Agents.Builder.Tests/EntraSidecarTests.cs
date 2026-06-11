// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Core;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.UserAuth.EntraSidecar;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests
{
    public class SidecarHttpClientTests
    {
        private static SidecarHttpClient CreateClient(HttpMessageHandler handler, string baseUrl = "http://localhost:5178")
        {
            var httpClient = new HttpClient(handler);
            return new SidecarHttpClient(httpClient, baseUrl);
        }

        private static Mock<HttpMessageHandler> CreateMockHandler(HttpStatusCode statusCode, string content)
        {
            var mock = new Mock<HttpMessageHandler>();
            mock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(content)
                });
            return mock;
        }

        [Fact]
        public async Task GetAuthorizationHeaderUnauthenticated_ReturnsToken()
        {
            var handler = CreateMockHandler(HttpStatusCode.OK, """{"authorizationHeader":"Bearer eyJtoken123"}""");
            var client = CreateClient(handler.Object);

            var result = await client.GetAuthorizationHeaderUnauthenticatedAsync("me");

            Assert.Equal("Bearer", result.Scheme);
            Assert.Equal("eyJtoken123", result.Token);
        }

        [Fact]
        public async Task GetAuthorizationHeaderUnauthenticated_WithPopScheme_ParsesCorrectly()
        {
            var handler = CreateMockHandler(HttpStatusCode.OK, """{"authorizationHeader":"PoP eyJpoptoken"}""");
            var client = CreateClient(handler.Object);

            var result = await client.GetAuthorizationHeaderUnauthenticatedAsync("me");

            Assert.Equal("PoP", result.Scheme);
            Assert.Equal("eyJpoptoken", result.Token);
        }

        [Fact]
        public async Task GetAuthorizationHeaderUnauthenticated_BuildsQueryParams()
        {
            HttpRequestMessage capturedRequest = null;
            var mock = new Mock<HttpMessageHandler>();
            mock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((r, _) => capturedRequest = r)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("""{"authorizationHeader":"Bearer token"}""")
                });

            var client = CreateClient(mock.Object);
            var options = new SidecarRequestOptions
            {
                AgentIdentity = "agent-app-id",
                AgentUsername = "user@contoso.com",
                Scopes = new List<string> { "User.Read", "Mail.Read" },
                RequestAppToken = true,
                Tenant = "tenant-guid",
                ForceRefresh = true
            };

            await client.GetAuthorizationHeaderUnauthenticatedAsync("graph", options);

            Assert.NotNull(capturedRequest);
            var url = capturedRequest.RequestUri.ToString();
            Assert.Contains("/AuthorizationHeaderUnauthenticated/graph", url);
            Assert.Contains("AgentIdentity=agent-app-id", url);
            Assert.Contains("AgentUsername=user%40contoso.com", url);
            Assert.Contains("optionsOverride.Scopes=User.Read", url);
            Assert.Contains("optionsOverride.Scopes=Mail.Read", url);
            Assert.Contains("optionsOverride.RequestAppToken=true", url);
            Assert.Contains("optionsOverride.AcquireTokenOptions.Tenant=tenant-guid", url);
            Assert.Contains("optionsOverride.AcquireTokenOptions.ForceRefresh=true", url);
        }

        [Fact]
        public async Task GetAuthorizationHeaderUnauthenticated_BothAgentUsernameAndUserId_Throws()
        {
            var handler = CreateMockHandler(HttpStatusCode.OK, """{"authorizationHeader":"Bearer token"}""");
            var client = CreateClient(handler.Object);
            var options = new SidecarRequestOptions
            {
                AgentIdentity = "agent-app-id",
                AgentUsername = "user@contoso.com",
                AgentUserId = "user-oid"
            };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => client.GetAuthorizationHeaderUnauthenticatedAsync("me", options));
        }

        [Fact]
        public async Task GetAuthorizationHeaderUnauthenticated_Error_ThrowsSidecarRequestException()
        {
            var problemJson = """{"type":"https://tools.ietf.org/html/rfc7231","title":"Bad Request","status":400,"detail":"Service not found"}""";
            var handler = CreateMockHandler(HttpStatusCode.BadRequest, problemJson);
            var client = CreateClient(handler.Object);

            var ex = await Assert.ThrowsAsync<SidecarRequestException>(
                () => client.GetAuthorizationHeaderUnauthenticatedAsync("unknown-service"));

            Assert.Equal(400, ex.StatusCode);
            Assert.Equal("Bad Request", ex.Message);
            Assert.NotNull(ex.ProblemDetails);
            Assert.Equal("Service not found", ex.ProblemDetails.Detail);
        }

        [Fact]
        public async Task GetAuthorizationHeaderUnauthenticated_MissingAuthHeader_Throws()
        {
            var handler = CreateMockHandler(HttpStatusCode.OK, """{"otherField":"value"}""");
            var client = CreateClient(handler.Object);

            await Assert.ThrowsAsync<SidecarRequestException>(
                () => client.GetAuthorizationHeaderUnauthenticatedAsync("me"));
        }

        [Fact]
        public async Task GetAuthorizationHeaderUnauthenticated_MalformedJson_ThrowsSidecarRequestException()
        {
            var handler = CreateMockHandler(HttpStatusCode.OK, "not-valid-json{");
            var client = CreateClient(handler.Object);

            var ex = await Assert.ThrowsAsync<SidecarRequestException>(
                () => client.GetAuthorizationHeaderUnauthenticatedAsync("me"));

            Assert.Equal(0, ex.StatusCode);
            Assert.Contains("unparsable", ex.Message);
        }

        [Fact]
        public async Task IsHealthyAsync_ReturnsTrue_WhenHealthy()
        {
            var handler = CreateMockHandler(HttpStatusCode.OK, "Healthy");
            var client = CreateClient(handler.Object);

            Assert.True(await client.IsHealthyAsync());
        }

        [Fact]
        public async Task IsHealthyAsync_ReturnsFalse_WhenUnhealthy()
        {
            var handler = CreateMockHandler(HttpStatusCode.ServiceUnavailable, "");
            var client = CreateClient(handler.Object);

            Assert.False(await client.IsHealthyAsync());
        }

        [Fact]
        public void ResolveBaseUrl_UsesEnvironmentVariable()
        {
            var original = Environment.GetEnvironmentVariable("SIDECAR_URL");
            try
            {
                Environment.SetEnvironmentVariable("SIDECAR_URL", "http://sidecar:9000");
                var url = SidecarHttpClient.ResolveBaseUrl(null);
                Assert.Equal("http://sidecar:9000", url);
            }
            finally
            {
                Environment.SetEnvironmentVariable("SIDECAR_URL", original);
            }
        }

        [Fact]
        public void ResolveBaseUrl_FallsBackToConfig()
        {
            var original = Environment.GetEnvironmentVariable("SIDECAR_URL");
            try
            {
                Environment.SetEnvironmentVariable("SIDECAR_URL", null);
                var url = SidecarHttpClient.ResolveBaseUrl("http://configured:5000");
                Assert.Equal("http://configured:5000", url);
            }
            finally
            {
                Environment.SetEnvironmentVariable("SIDECAR_URL", original);
            }
        }

        [Fact]
        public void ResolveBaseUrl_FallsBackToDefault()
        {
            var original = Environment.GetEnvironmentVariable("SIDECAR_URL");
            try
            {
                Environment.SetEnvironmentVariable("SIDECAR_URL", null);
                var url = SidecarHttpClient.ResolveBaseUrl(null);
                Assert.Equal("http://localhost:5178", url);
            }
            finally
            {
                Environment.SetEnvironmentVariable("SIDECAR_URL", original);
            }
        }

        [Fact]
        public async Task GetAuthorizationHeaderUnauthenticated_AgentUserId_BuildsCorrectQuery()
        {
            HttpRequestMessage capturedRequest = null;
            var mock = new Mock<HttpMessageHandler>();
            mock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((r, _) => capturedRequest = r)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("""{"authorizationHeader":"Bearer token"}""")
                });

            var client = CreateClient(mock.Object);
            var options = new SidecarRequestOptions
            {
                AgentIdentity = "agent-id",
                AgentUserId = "user-object-id"
            };

            await client.GetAuthorizationHeaderUnauthenticatedAsync("me", options);

            var url = capturedRequest.RequestUri.ToString();
            Assert.Contains("AgentIdentity=agent-id", url);
            Assert.Contains("AgentUserId=user-object-id", url);
            Assert.DoesNotContain("AgentUsername", url);
        }
    }

    public class SidecarAccessTokenProviderTests
    {
        private static (SidecarAccessTokenProvider provider, Mock<HttpMessageHandler> handler) CreateProvider(
            string responseToken = "test-token",
            string serviceName = "default")
        {
            var mock = new Mock<HttpMessageHandler>();
            mock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent($"{{\"authorizationHeader\":\"Bearer {responseToken}\"}}")
                });

            var httpClient = new HttpClient(mock.Object);
            var sidecarClient = new SidecarHttpClient(httpClient, "http://localhost:5178");
            var settings = new SidecarConnectionSettings { ServiceName = serviceName };
            var provider = new SidecarAccessTokenProvider(sidecarClient, settings);

            return (provider, mock);
        }

        [Fact]
        public async Task GetAccessTokenAsync_CallsUnauthenticatedEndpoint()
        {
            var (provider, handler) = CreateProvider("app-token");

            var token = await provider.GetAccessTokenAsync("https://graph.microsoft.com",
                new List<string> { "User.Read" });

            Assert.Equal("app-token", token);

            handler.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(r =>
                    r.RequestUri.ToString().Contains("/AuthorizationHeaderUnauthenticated/") &&
                    r.RequestUri.ToString().Contains("optionsOverride.RequestAppToken=true") &&
                    r.RequestUri.ToString().Contains("optionsOverride.Scopes=User.Read")),
                ItExpr.IsAny<CancellationToken>());
        }

        private static (SidecarAccessTokenProvider provider, List<string> capturedUrls) CreateCapturingProvider(
            string serviceName = "default", string responseToken = "sidecar-token")
        {
            var capturedUrls = new List<string>();
            var mock = new Mock<HttpMessageHandler>();
            mock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((r, _) => capturedUrls.Add(r.RequestUri.ToString()))
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent($"{{\"authorizationHeader\":\"Bearer {responseToken}\"}}")
                });

            var sidecarClient = new SidecarHttpClient(new HttpClient(mock.Object), "http://localhost:5178");
            var settings = new SidecarConnectionSettings { ServiceName = serviceName };
            var provider = new SidecarAccessTokenProvider(sidecarClient, settings);

            return (provider, capturedUrls);
        }

        [Fact]
        public async Task GetAgenticApplicationTokenAsync_RequestsBlueprintTokenFromBlueprintService()
        {
            var (provider, urls) = CreateCapturingProvider(responseToken: "blueprint-token");

            var token = await provider.GetAgenticApplicationTokenAsync("tenant-id", "agent-app-id");

            Assert.Equal("blueprint-token", token);
            var url = Assert.Single(urls);
            // Blueprint token comes from the dedicated app-only token-exchange downstream API.
            Assert.Contains("/AuthorizationHeaderUnauthenticated/agenticblueprint", url);
            Assert.Contains("AgentIdentity=agent-app-id", url);
            Assert.Contains("optionsOverride.AcquireTokenOptions.Tenant=tenant-id", url);
            // Scope and app-only are sidecar-side config, not client overrides.
            Assert.DoesNotContain("optionsOverride.Scopes=", url);
            Assert.DoesNotContain("optionsOverride.RequestAppToken=true", url);
        }

        [Fact]
        public async Task GetAgenticInstanceTokenAsync_RequestsAppOnlyResourceToken()
        {
            var (provider, urls) = CreateCapturingProvider(responseToken: "instance-token");

            var token = await provider.GetAgenticInstanceTokenAsync("tenant-id", "agent-app-id");

            Assert.Equal("instance-token", token);
            var url = Assert.Single(urls);
            // The sidecar performs the full Blueprint -> Instance chain; autonomous = app-only resource token.
            Assert.Contains("/AuthorizationHeaderUnauthenticated/default", url);
            Assert.Contains("AgentIdentity=agent-app-id", url);
            Assert.Contains("optionsOverride.RequestAppToken=true", url);
            Assert.DoesNotContain("AgentUsername", url);
            Assert.DoesNotContain("AgentUserId", url);
        }

        [Fact]
        public async Task GetAgenticUserTokenAsync_PassesAgentIdentityAndUserObjectId()
        {
            var (provider, urls) = CreateCapturingProvider("botframework", "user-token");

            var token = await provider.GetAgenticUserTokenAsync(
                "tenant-id", "agent-app-id", "b42f6097-96e6-41c0-b954-3bc58566c2d3",
                new List<string> { "5a807f24-c9de-44ee-a3a7-329e88a00ffc/.default" });

            Assert.Equal("user-token", token);
            // The sidecar performs the full Blueprint -> Instance -> agentic User chain internally.
            var url = Assert.Single(urls);
            Assert.Contains("/AuthorizationHeaderUnauthenticated/botframework", url);
            Assert.Contains("AgentIdentity=agent-app-id", url);
            // GUID agentic user is passed as AgentUserId (object id), not AgentUsername.
            Assert.Contains("AgentUserId=b42f6097-96e6-41c0-b954-3bc58566c2d3", url);
            Assert.DoesNotContain("AgentUsername", url);
            Assert.Contains("optionsOverride.AcquireTokenOptions.Tenant=tenant-id", url);
            // Resource scope is NOT app-only for the user flow.
            Assert.DoesNotContain("optionsOverride.RequestAppToken=true", url);
        }

        [Fact]
        public async Task GetAgenticUserTokenAsync_WithUpn_PassesAgentUsername()
        {
            var (provider, urls) = CreateCapturingProvider("botframework", "user-token");

            await provider.GetAgenticUserTokenAsync(
                "tenant-id", "agent-app-id", "user@contoso.com",
                new List<string> { "User.Read" });

            var url = Assert.Single(urls);
            Assert.Contains("AgentUsername=user%40contoso.com", url);
            Assert.DoesNotContain("AgentUserId", url);
        }

        [Fact]
        public void GetTokenCredential_ReturnsNonNull()
        {
            var (provider, _) = CreateProvider();
            var credential = provider.GetTokenCredential();
            Assert.NotNull(credential);
        }

        [Fact]
        public async Task GetAccessTokenAsync_ForceRefresh_AddsForceRefreshOverride()
        {
            var (provider, urls) = CreateCapturingProvider();

            await provider.GetAccessTokenAsync("https://graph.microsoft.com",
                new List<string> { "User.Read" }, forceRefresh: true);

            var url = Assert.Single(urls);
            Assert.Contains("optionsOverride.AcquireTokenOptions.ForceRefresh=true", url);
        }

        [Fact]
        public async Task GetAgenticUserTokenAsync_NullScopes_FallsBackToSettingsScopes()
        {
            var capturedUrls = new List<string>();
            var mock = new Mock<HttpMessageHandler>();
            mock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((r, _) => capturedUrls.Add(r.RequestUri.ToString()))
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("""{"authorizationHeader":"Bearer t"}""")
                });

            var sidecarClient = new SidecarHttpClient(new HttpClient(mock.Object), "http://localhost:5178");
            var provider = new SidecarAccessTokenProvider(sidecarClient,
                new SidecarConnectionSettings { ServiceName = "botframework", Scopes = new List<string> { "Configured.Scope" } });

            await provider.GetAgenticUserTokenAsync("tenant-id", "agent-app-id", "user@contoso.com", scopes: null);

            var url = Assert.Single(capturedUrls);
            Assert.Contains("optionsOverride.Scopes=Configured.Scope", url);
        }

        [Fact]
        public async Task ConfigLoaderConstructor_ReusesRegisteredClientAndBindsServiceName()
        {
            var capturedUrls = new List<string>();
            var mock = new Mock<HttpMessageHandler>();
            mock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((r, _) => capturedUrls.Add(r.RequestUri.ToString()))
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("""{"authorizationHeader":"Bearer t"}""")
                });

            var services = new ServiceCollection();
            services.AddSingleton(new SidecarHttpClient(new HttpClient(mock.Object), "http://localhost:5178"));
            var sp = services.BuildServiceProvider();

            var section = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string> { { "EntraSidecar:ServiceName", "botframework" } })
                .Build()
                .GetSection("EntraSidecar");

            var provider = new SidecarAccessTokenProvider(sp, section);

            await provider.GetAccessTokenAsync("https://graph.microsoft.com", new List<string> { "User.Read" });

            // Verifies the config-loader ctor both reused the DI client and bound ServiceName from config.
            var url = Assert.Single(capturedUrls);
            Assert.Contains("/AuthorizationHeaderUnauthenticated/botframework", url);
        }
    }

    public class SidecarTokenCredentialTests
    {
        private static SidecarAccessTokenProvider CreateProvider(string responseToken)
        {
            var mock = new Mock<HttpMessageHandler>();
            mock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent($"{{\"authorizationHeader\":\"Bearer {responseToken}\"}}")
                });

            var sidecarClient = new SidecarHttpClient(new HttpClient(mock.Object), "http://localhost:5178");
            return new SidecarAccessTokenProvider(sidecarClient, new SidecarConnectionSettings { ServiceName = "default" });
        }

        private static string CreateJwtWithExpiry(DateTimeOffset expiry)
        {
            // Unsigned JWT carrying only an "exp" claim, matching what the credential reads back.
            var token = new JwtSecurityToken(issuer: null, audience: null, claims: null,
                notBefore: null, expires: expiry.UtcDateTime);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        [Fact]
        public async Task GetTokenAsync_JwtToken_UsesExpClaimForExpiry()
        {
            var expectedExpiry = DateTimeOffset.UtcNow.AddHours(1);
            var jwt = CreateJwtWithExpiry(expectedExpiry);
            var credential = CreateProvider(jwt).GetTokenCredential();

            var token = await credential.GetTokenAsync(
                new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" }), CancellationToken.None);

            Assert.Equal(jwt, token.Token);
            // "exp" is second-precision, so allow a small delta.
            Assert.True(System.Math.Abs((token.ExpiresOn - expectedExpiry).TotalSeconds) < 2,
                $"Expected expiry near {expectedExpiry:o} but was {token.ExpiresOn:o}");
        }

        [Fact]
        public async Task GetTokenAsync_OpaqueToken_UsesFallbackLifetime()
        {
            var credential = CreateProvider("opaque-non-jwt-token").GetTokenCredential();

            var before = DateTimeOffset.UtcNow;
            var token = await credential.GetTokenAsync(
                new TokenRequestContext(new[] { "scope" }), CancellationToken.None);
            var after = DateTimeOffset.UtcNow;

            Assert.Equal("opaque-non-jwt-token", token.Token);
            // Fallback lifetime is 5 minutes from "now".
            Assert.InRange(token.ExpiresOn, before.AddMinutes(5).AddSeconds(-5), after.AddMinutes(5).AddSeconds(5));
        }

        [Fact]
        public void GetToken_Synchronous_ReturnsToken()
        {
            var jwt = CreateJwtWithExpiry(DateTimeOffset.UtcNow.AddHours(1));
            var credential = CreateProvider(jwt).GetTokenCredential();

            var token = credential.GetToken(
                new TokenRequestContext(new[] { "scope" }), CancellationToken.None);

            Assert.Equal(jwt, token.Token);
        }
    }

    public class SidecarHealthCheckTests
    {
        private static SidecarHttpClient CreateClient(HttpStatusCode healthzStatus)
        {
            var mock = new Mock<HttpMessageHandler>();
            mock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage { StatusCode = healthzStatus });
            return new SidecarHttpClient(new HttpClient(mock.Object), "http://localhost:5178");
        }

        private static HealthCheckContext CreateContext(HealthStatus failureStatus = HealthStatus.Unhealthy)
        {
            return new HealthCheckContext
            {
                Registration = new HealthCheckRegistration("entra_sidecar", new Mock<IHealthCheck>().Object, failureStatus, null)
            };
        }

        [Fact]
        public async Task CheckHealthAsync_Reachable_ReturnsHealthy()
        {
            var check = new SidecarHealthCheck(CreateClient(HttpStatusCode.OK));

            var result = await check.CheckHealthAsync(CreateContext());

            Assert.Equal(HealthStatus.Healthy, result.Status);
        }

        [Fact]
        public async Task CheckHealthAsync_Unreachable_ReturnsConfiguredFailureStatus()
        {
            var check = new SidecarHealthCheck(CreateClient(HttpStatusCode.ServiceUnavailable));

            var result = await check.CheckHealthAsync(CreateContext(HealthStatus.Degraded));

            Assert.Equal(HealthStatus.Degraded, result.Status);
        }

        [Fact]
        public void AddSidecarHealthCheck_RegistersNamedCheck_ReusingRegisteredClient()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new SidecarHttpClient(new HttpClient(), "http://localhost:5178"));
            services.AddHealthChecks().AddSidecarHealthCheck();

            var sp = services.BuildServiceProvider();
            var options = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

            var registration = Assert.Single(options.Registrations, r => r.Name == "entra_sidecar");
            Assert.IsType<SidecarHealthCheck>(registration.Factory(sp));
        }

        [Fact]
        public void AddSidecarHealthCheck_CreatesClient_WhenNoneRegistered()
        {
            var services = new ServiceCollection();
            services.AddHealthChecks().AddSidecarHealthCheck(name: "custom_sidecar");

            var sp = services.BuildServiceProvider();
            var options = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

            var registration = Assert.Single(options.Registrations, r => r.Name == "custom_sidecar");
            Assert.IsType<SidecarHealthCheck>(registration.Factory(sp));
        }
    }

    public class SidecarUserAuthorizationTests
    {
        private static (SidecarUserAuthorization handler, List<string> urls) CreateHandler(
            string serviceName = "me", string responseToken = "user-token")
        {
            var urls = new List<string>();
            var mock = new Mock<HttpMessageHandler>();
            mock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((r, _) => urls.Add(r.RequestUri.ToString()))
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent($"{{\"authorizationHeader\":\"Bearer {responseToken}\"}}")
                });

            var sidecarClient = new SidecarHttpClient(new HttpClient(mock.Object), "http://localhost:5178");
            var settings = new SidecarSettings { ServiceName = serviceName };
            var handler = new SidecarUserAuthorization("test", new Mock<IConnections>().Object, settings, sidecarClient);
            return (handler, urls);
        }

        private static ITurnContext CreateAgenticContext(string agenticUserId = null, string id = null)
        {
            var activity = new Activity
            {
                Recipient = new ChannelAccount
                {
                    Role = RoleTypes.AgenticUser,
                    AgenticAppId = "agent-app-id",
                    AgenticUserId = agenticUserId,
                    Id = id
                }
            };
            var context = new Mock<ITurnContext>();
            context.Setup(c => c.Activity).Returns(activity);
            return context.Object;
        }

        [Fact]
        public async Task GetRefreshedUserTokenAsync_AgenticUserId_PassesAgentUserId()
        {
            var (handler, urls) = CreateHandler();
            var context = CreateAgenticContext(agenticUserId: "user-oid");

            var response = await handler.GetRefreshedUserTokenAsync(context);

            Assert.Equal("user-token", response.Token);
            var url = Assert.Single(urls);
            Assert.Contains("/AuthorizationHeaderUnauthenticated/me", url);
            Assert.Contains("AgentIdentity=agent-app-id", url);
            Assert.Contains("AgentUserId=user-oid", url);
            Assert.DoesNotContain("AgentUsername", url);
        }

        [Fact]
        public async Task GetRefreshedUserTokenAsync_UsesUnauthenticatedEndpoint_ByDefault()
        {
            var (handler, urls) = CreateHandler();

            await handler.GetRefreshedUserTokenAsync(CreateAgenticContext(agenticUserId: "user-oid"));

            var url = Assert.Single(urls);
            Assert.Contains("/AuthorizationHeaderUnauthenticated/me", url);
        }

        [Fact]
        public async Task GetRefreshedUserTokenAsync_NoAgenticUserId_PassesAgentUsernameFromId()
        {
            var (handler, urls) = CreateHandler();
            var context = CreateAgenticContext(agenticUserId: null, id: "user@contoso.com");

            await handler.GetRefreshedUserTokenAsync(context);

            var url = Assert.Single(urls);
            Assert.Contains("AgentUsername=user%40contoso.com", url);
            Assert.DoesNotContain("AgentUserId", url);
        }

        [Fact]
        public async Task SignInUserAsync_NonAgenticRequest_Throws()
        {
            var (handler, _) = CreateHandler();
            var activity = new Activity { Recipient = new ChannelAccount { Role = RoleTypes.User } };
            var context = new Mock<ITurnContext>();
            context.Setup(c => c.Activity).Returns(activity);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => handler.SignInUserAsync(context.Object));
        }

        [Fact]
        public async Task GetRefreshedUserTokenAsync_SidecarError_ThrowsWrappedException()
        {
            var mock = new Mock<HttpMessageHandler>();
            mock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Content = new StringContent("""{"title":"Bad Request","status":400,"detail":"nope"}""")
                });
            var sidecarClient = new SidecarHttpClient(new HttpClient(mock.Object), "http://localhost:5178");
            var handler = new SidecarUserAuthorization("test", new Mock<IConnections>().Object,
                new SidecarSettings { ServiceName = "me" }, sidecarClient);
            var context = CreateAgenticContext(agenticUserId: "user-oid");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => handler.GetRefreshedUserTokenAsync(context));
        }

        [Fact]
        public void Constructor_NullName_Throws()
        {
            var sidecarClient = new SidecarHttpClient(new HttpClient(), "http://localhost:5178");
            Assert.Throws<ArgumentNullException>(() =>
                new SidecarUserAuthorization(null, new Mock<IConnections>().Object,
                    new SidecarSettings { ServiceName = "me" }, sidecarClient));
        }

        private static (SidecarUserAuthorization handler, List<string> urls) CreateHandler(SidecarSettings settings)
        {
            var urls = new List<string>();
            var mock = new Mock<HttpMessageHandler>();
            mock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((r, _) => urls.Add(r.RequestUri.ToString()))
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("""{"authorizationHeader":"Bearer user-token"}""")
                });

            var sidecarClient = new SidecarHttpClient(new HttpClient(mock.Object), "http://localhost:5178");
            var handler = new SidecarUserAuthorization("test", new Mock<IConnections>().Object, settings, sidecarClient);
            return (handler, urls);
        }

        [Fact]
        public async Task GetRefreshedUserTokenAsync_AppliesSettingsToRequest()
        {
            var settings = new SidecarSettings
            {
                ServiceName = "me",
                ForceRefresh = true,
                RequestAppToken = true,
                Tenant = "tenant-xyz",
                Scopes = new List<string> { "User.Read" }
            };
            var (handler, urls) = CreateHandler(settings);

            await handler.GetRefreshedUserTokenAsync(CreateAgenticContext(agenticUserId: "user-oid"));

            var url = Assert.Single(urls);
            Assert.Contains("optionsOverride.AcquireTokenOptions.ForceRefresh=true", url);
            Assert.Contains("optionsOverride.RequestAppToken=true", url);
            Assert.Contains("optionsOverride.AcquireTokenOptions.Tenant=tenant-xyz", url);
            Assert.Contains("optionsOverride.Scopes=User.Read", url);
        }

        [Fact]
        public async Task GetRefreshedUserTokenAsync_ExchangeScopesOverrideSettingsScopes()
        {
            var settings = new SidecarSettings
            {
                ServiceName = "me",
                Scopes = new List<string> { "User.Read" }
            };
            var (handler, urls) = CreateHandler(settings);

            await handler.GetRefreshedUserTokenAsync(
                CreateAgenticContext(agenticUserId: "user-oid"),
                exchangeScopes: new List<string> { "Mail.Read" });

            var url = Assert.Single(urls);
            Assert.Contains("optionsOverride.Scopes=Mail.Read", url);
            Assert.DoesNotContain("optionsOverride.Scopes=User.Read", url);
        }

        [Fact]
        public async Task GetRefreshedUserTokenAsync_AgenticIdentityRole_OmitsUserParameters()
        {
            var (handler, urls) = CreateHandler();
            var activity = new Activity
            {
                Recipient = new ChannelAccount
                {
                    Role = RoleTypes.AgenticIdentity,
                    AgenticAppId = "agent-app-id",
                    AgenticUserId = "should-be-ignored",
                    Id = "ignored@contoso.com"
                }
            };
            var context = new Mock<ITurnContext>();
            context.Setup(c => c.Activity).Returns(activity);

            await handler.GetRefreshedUserTokenAsync(context.Object);

            var url = Assert.Single(urls);
            Assert.Contains("AgentIdentity=agent-app-id", url);
            Assert.DoesNotContain("AgentUserId", url);
            Assert.DoesNotContain("AgentUsername", url);
        }

        [Fact]
        public void Constructor_EmptyServiceName_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new SidecarUserAuthorization("test", new Mock<IConnections>().Object,
                    new SidecarSettings { ServiceName = "" }));
        }
    }

    public class SidecarServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddSidecarConnections_RegistersProvidersAsSameSingleton()
        {
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();

            services.AddSidecarConnections(configuration);

            var sp = services.BuildServiceProvider();

            var accessProvider = sp.GetRequiredService<IAccessTokenProvider>();
            var agenticProvider = sp.GetRequiredService<IAgenticTokenProvider>();
            var concrete = sp.GetRequiredService<SidecarAccessTokenProvider>();

            Assert.Same(concrete, accessProvider);
            Assert.Same(concrete, agenticProvider);
            Assert.NotNull(sp.GetRequiredService<SidecarHttpClient>());
        }

        [Fact]
        public async Task AddSidecarConnections_PreservesBlueprintServiceNameAndScopes()
        {
            var capturedUrls = new List<string>();
            var mock = new Mock<HttpMessageHandler>();
            mock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((r, _) => capturedUrls.Add(r.RequestUri.ToString()))
                .ReturnsAsync(() => new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("""{"authorizationHeader":"Bearer token"}""")
                });

            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                { "EntraSidecar:ServiceName", "botframework" },
                { "EntraSidecar:BlueprintServiceName", "myblueprint" },
                { "EntraSidecar:Scopes:0", "Custom.Scope" }
            }).Build();

            var services = new ServiceCollection();
            services.AddSidecarConnections(configuration);
            services.AddHttpClient(SidecarHttpClient.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => mock.Object);

            var sp = services.BuildServiceProvider();
            var provider = sp.GetRequiredService<SidecarAccessTokenProvider>();

            // BlueprintServiceName must survive registration, else it falls back to "agenticblueprint".
            await provider.GetAgenticApplicationTokenAsync("tenant-1", "agent-1");
            Assert.Contains(capturedUrls, u => u.Contains("/AuthorizationHeaderUnauthenticated/myblueprint"));
            Assert.DoesNotContain(capturedUrls, u => u.Contains("agenticblueprint"));

            // Scopes must survive on the connection settings (used by the resource/instance token call).
            capturedUrls.Clear();
            await provider.GetAgenticInstanceTokenAsync("tenant-1", "agent-1");
            Assert.Contains(capturedUrls, u => u.Contains("optionsOverride.Scopes=Custom.Scope"));
        }
    }

    public class SidecarStartupHealthCheckTests
    {
        private static SidecarHttpClient CreateClient(HttpStatusCode healthzStatus)
        {
            var mock = new Mock<HttpMessageHandler>();
            mock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage { StatusCode = healthzStatus });
            return new SidecarHttpClient(new HttpClient(mock.Object), "http://localhost:5178");
        }

        [Fact]
        public async Task StartAsync_Healthy_DoesNotThrow()
        {
            var check = new SidecarStartupHealthCheck(
                CreateClient(HttpStatusCode.OK), failOnUnreachable: true, TimeSpan.FromSeconds(5));

            await check.StartAsync(CancellationToken.None);
        }

        [Fact]
        public async Task StartAsync_Unreachable_FailOnUnreachable_Throws()
        {
            var check = new SidecarStartupHealthCheck(
                CreateClient(HttpStatusCode.ServiceUnavailable), failOnUnreachable: true, TimeSpan.FromSeconds(5));

            await Assert.ThrowsAsync<InvalidOperationException>(() => check.StartAsync(CancellationToken.None));
        }

        [Fact]
        public async Task StartAsync_Unreachable_WarnOnly_DoesNotThrow()
        {
            var check = new SidecarStartupHealthCheck(
                CreateClient(HttpStatusCode.ServiceUnavailable), failOnUnreachable: false, TimeSpan.FromSeconds(5));

            await check.StartAsync(CancellationToken.None);
        }

        [Fact]
        public async Task StopAsync_Completes()
        {
            var check = new SidecarStartupHealthCheck(
                CreateClient(HttpStatusCode.OK), failOnUnreachable: false, TimeSpan.FromSeconds(5));

            await check.StopAsync(CancellationToken.None);
        }
    }

    public class SidecarConnectionSettingsTests
    {
        private static IConfigurationSection Section(Dictionary<string, string> values)
        {
            return new ConfigurationBuilder().AddInMemoryCollection(values).Build().GetSection("Conn");
        }

        [Fact]
        public void ConfigConstructor_BindsServiceNameAndBlueprint()
        {
            var section = Section(new Dictionary<string, string>
            {
                { "Conn:ServiceName", "botframework" },
                { "Conn:BlueprintServiceName", "myblueprint" },
                { "Conn:TenantId", "tenant-1" }
            });

            var settings = new SidecarConnectionSettings(section);

            Assert.Equal("botframework", settings.ServiceName);
            Assert.Equal("myblueprint", settings.BlueprintServiceName);
            Assert.Equal("tenant-1", settings.TenantId);
        }

        [Fact]
        public void ConfigConstructor_AppliesDefaults_WhenKeysAbsent()
        {
            var section = Section(new Dictionary<string, string> { { "Conn:TenantId", "tenant-1" } });

            var settings = new SidecarConnectionSettings(section);

            Assert.Equal("default", settings.ServiceName);
            Assert.Equal("agenticblueprint", settings.BlueprintServiceName);
        }

        [Fact]
        public void ConfigConstructor_NonExistentSection_Throws()
        {
            var section = new ConfigurationBuilder().Build().GetSection("Missing");
            Assert.Throws<ArgumentException>(() => new SidecarConnectionSettings(section));
        }
    }
}
