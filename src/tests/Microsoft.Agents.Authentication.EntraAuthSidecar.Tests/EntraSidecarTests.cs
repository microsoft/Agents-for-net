// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Core;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Authentication.EntraAuthSidecar;
using Microsoft.Agents.Authentication.EntraAuthSidecar.HealthChecks;
using Microsoft.Agents.Authentication.EntraAuthSidecar.Model;
using Microsoft.Agents.Authentication.EntraAuthSidecar.Utils;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Authentication.EntraAuthSidecar.Tests
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
        public async Task GetAuthorizationHeaderUnauthenticated_HeaderWithoutScheme_DefaultsToBearer()
        {
            // A header value with no space (raw token) defaults the scheme to "Bearer".
            var handler = CreateMockHandler(HttpStatusCode.OK, """{"authorizationHeader":"rawtokenwithoutscheme"}""");
            var client = CreateClient(handler.Object);

            var result = await client.GetAuthorizationHeaderUnauthenticatedAsync("me");

            Assert.Equal("Bearer", result.Scheme);
            Assert.Equal("rawtokenwithoutscheme", result.Token);
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
        public async Task GetAuthorizationHeaderUnauthenticated_Error_ThrowsErrorResponseException()
        {
            var problemJson = """{"type":"https://tools.ietf.org/html/rfc7231","title":"Bad Request","status":400,"detail":"Service not found"}""";
            var handler = CreateMockHandler(HttpStatusCode.BadRequest, problemJson);
            var client = CreateClient(handler.Object);

            var ex = await Assert.ThrowsAsync<ErrorResponseException>(
                () => client.GetAuthorizationHeaderUnauthenticatedAsync("unknown-service"));

            Assert.Equal(400, ex.StatusCode);
            // Title and detail are surfaced through the public exception message.
            Assert.Contains("Bad Request", ex.Message);
            Assert.Contains("Service not found", ex.Message);
        }

        [Fact]
        public async Task GetAuthorizationHeaderUnauthenticated_MissingAuthHeader_Throws()
        {
            var handler = CreateMockHandler(HttpStatusCode.OK, """{"otherField":"value"}""");
            var client = CreateClient(handler.Object);

            await Assert.ThrowsAsync<ErrorResponseException>(
                () => client.GetAuthorizationHeaderUnauthenticatedAsync("me"));
        }

        [Fact]
        public async Task GetAuthorizationHeaderUnauthenticated_MalformedJson_ThrowsErrorResponseException()
        {
            var handler = CreateMockHandler(HttpStatusCode.OK, "not-valid-json{");
            var client = CreateClient(handler.Object);

            var ex = await Assert.ThrowsAsync<ErrorResponseException>(
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

    public class SidecarAuthTests
    {
        private static (SidecarAuth provider, Mock<HttpMessageHandler> handler) CreateProvider(
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
            var provider = new SidecarAuth(sidecarClient, settings);

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

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GetAccessTokenAsync_BlankServiceName_FallsBackToDefault(string serviceName)
        {
            var (provider, urls) = CreateCapturingProvider(serviceName: serviceName);

            await provider.GetAccessTokenAsync("https://graph.microsoft.com", new List<string> { "User.Read" });

            var url = Assert.Single(urls);
            // A blank ServiceName must not produce /AuthorizationHeaderUnauthenticated/ with a missing segment.
            Assert.Contains("/AuthorizationHeaderUnauthenticated/default", url);
        }

        [Fact]
        public async Task GetCachedToken_ScopeOrderDoesNotAffectCacheKey()
        {
            var (provider, urls) = CreateCapturingProvider(responseToken: "cached-token");

            await provider.GetAccessTokenAsync("https://graph.microsoft.com",
                new List<string> { "User.Read", "Mail.Read" });
            // Same scope set, different order (and a duplicate) must hit the same cache entry.
            await provider.GetAccessTokenAsync("https://graph.microsoft.com",
                new List<string> { "Mail.Read", "User.Read", "User.Read" });

            Assert.Single(urls);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GetAgenticApplicationTokenAsync_BlankBlueprintServiceName_FallsBackToDefault(string blueprintServiceName)
        {
            var (provider, urls) = CreateCapturingProvider(blueprintServiceName: blueprintServiceName);

            await provider.GetAgenticApplicationTokenAsync("tenant-id", "agent-app-id");

            var url = Assert.Single(urls);
            // A blank BlueprintServiceName must fall back to the default downstream API name.
            Assert.Contains("/AuthorizationHeaderUnauthenticated/agenticblueprint", url);
        }

        private static (SidecarAuth provider, List<string> capturedUrls) CreateCapturingProvider(
            string serviceName = "default", string responseToken = "sidecar-token", string blueprintServiceName = "agenticblueprint")
        {
            var capturedUrls = new List<string>();
            var mock = new Mock<HttpMessageHandler>();
            mock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((r, _) => capturedUrls.Add(r.RequestUri.ToString()))
                .ReturnsAsync(() => new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent($"{{\"authorizationHeader\":\"Bearer {responseToken}\"}}")
                });

            var sidecarClient = new SidecarHttpClient(new HttpClient(mock.Object), "http://localhost:5178");
            var settings = new SidecarConnectionSettings { ServiceName = serviceName, BlueprintServiceName = blueprintServiceName };
            var provider = new SidecarAuth(sidecarClient, settings);

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
            var provider = new SidecarAuth(sidecarClient,
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

            var provider = new SidecarAuth(sp, section);

            await provider.GetAccessTokenAsync("https://graph.microsoft.com", new List<string> { "User.Read" });

            // Verifies the config-loader ctor both reused the DI client and bound ServiceName from config.
            var url = Assert.Single(capturedUrls);
            Assert.Contains("/AuthorizationHeaderUnauthenticated/botframework", url);
        }

        [Fact]
        public void ConfigLoaderConstructor_CreatesClient_WhenNoneRegistered()
        {
            // No SidecarHttpClient and no IHttpClientFactory registered -> the ctor must create a plain
            // HttpClient against the (loopback) resolved base URL, which passes the SSRF guard.
            var sp = new ServiceCollection().BuildServiceProvider();

            var section = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "EntraSidecar:ServiceName", "botframework" },
                    { "EntraSidecar:RequestTimeout", "00:00:03" },
                    { "EntraSidecar:RetryCount", "1" }
                })
                .Build()
                .GetSection("EntraSidecar");

            var provider = new SidecarAuth(sp, section);

            Assert.NotNull(provider);
        }

        [Fact]
        public void ConfigSectionConstructor_BindsSettings_AndExposesConnectionSettings()
        {
            var section = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "EntraSidecar:TenantId", "tenant-xyz" },
                    { "EntraSidecar:Scopes:0", "Custom.Scope" }
                })
                .Build()
                .GetSection("EntraSidecar");

            var provider = new SidecarAuth(new SidecarHttpClient(new HttpClient(), "http://localhost:5178"), section);

            var settings = provider.ConnectionSettings;
            Assert.Equal("tenant-xyz", settings.TenantId);
            Assert.Contains("Custom.Scope", settings.Scopes);
        }

        [Fact]
        public async Task TokenCache_PrunesExpiredEntries_WhenGrowthExceedsCap()
        {
            // Every issued token is already expired, so each distinct-user entry is reclaimed by the
            // expired-entry sweep that runs on write once the cache passes its hard cap.
            var pastJwt = CreateJwtWithExpiry(DateTimeOffset.UtcNow.AddMinutes(-5));
            var (provider, _) = CreateCapturingProvider(responseToken: pastJwt);

            // Drive many distinct users (distinct cache keys) well past the 500-entry cap.
            for (var i = 0; i < 700; i++)
            {
                await provider.GetAgenticUserTokenAsync(
                    "tenant", "agent", $"user{i}@contoso.com", new List<string> { "scope" });
            }

            var count = GetTokenCacheCount(provider);
            Assert.True(count <= 500, $"Token cache grew unbounded: {count} entries.");
        }

        [Fact]
        public async Task TokenCache_EnforcesHardCap_WhenAllEntriesAreValid()
        {
            // Tokens are valid (future expiry), so the expired-entry sweep frees nothing; the hard cap
            // must still bound the cache by evicting the entries nearest to expiry.
            var futureJwt = CreateJwtWithExpiry(DateTimeOffset.UtcNow.AddHours(1));
            var (provider, _) = CreateCapturingProvider(responseToken: futureJwt);

            for (var i = 0; i < 700; i++)
            {
                await provider.GetAgenticUserTokenAsync(
                    "tenant", "agent", $"user{i}@contoso.com", new List<string> { "scope" });
            }

            var count = GetTokenCacheCount(provider);
            Assert.True(count <= 500, $"Token cache exceeded its hard cap: {count} entries.");
        }

        private static int GetTokenCacheCount(SidecarAuth provider)
        {
            var field = typeof(SidecarAuth).GetField("_tokenCache", BindingFlags.NonPublic | BindingFlags.Instance);
            var cache = (System.Collections.IDictionary)field.GetValue(provider);
            return cache.Count;
        }

        private static string CreateJwtWithExpiry(DateTimeOffset expiry)
        {
            var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
                issuer: null, audience: null, claims: null, notBefore: null, expires: expiry.UtcDateTime);
            return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
        }

        [Fact]
        public async Task GetAccessTokenAsync_SecondCall_ServedFromCache()
        {
            var (provider, urls) = CreateCapturingProvider(responseToken: "opaque-token");

            var first = await provider.GetAccessTokenAsync("https://graph.microsoft.com", new List<string> { "User.Read" });
            var second = await provider.GetAccessTokenAsync("https://graph.microsoft.com", new List<string> { "User.Read" });

            Assert.Equal("opaque-token", first);
            Assert.Equal(first, second);
            // Opaque token => 5-minute fallback TTL, so the second call is served from cache (single sidecar hit).
            Assert.Single(urls);
        }

        [Fact]
        public async Task GetAccessTokenAsync_ForceRefresh_BypassesCache()
        {
            var (provider, urls) = CreateCapturingProvider(responseToken: "opaque-token");

            await provider.GetAccessTokenAsync("https://graph.microsoft.com", new List<string> { "User.Read" });
            await provider.GetAccessTokenAsync("https://graph.microsoft.com", new List<string> { "User.Read" }, forceRefresh: true);

            // Force refresh evicts the cached entry and re-acquires from the sidecar.
            Assert.Equal(2, urls.Count);
        }

        [Fact]
        public async Task GetAccessTokenAsync_DifferentScopes_UseDistinctCacheEntries()
        {
            var (provider, urls) = CreateCapturingProvider(responseToken: "opaque-token");

            await provider.GetAccessTokenAsync("https://graph.microsoft.com", new List<string> { "User.Read" });
            await provider.GetAccessTokenAsync("https://graph.microsoft.com", new List<string> { "Mail.Read" });

            // Distinct scopes are distinct cache keys, so each triggers its own sidecar call.
            Assert.Equal(2, urls.Count);
        }

        [Fact]
        public async Task GetAccessTokenAsync_NearExpiryJwt_IsNotServedFromCache()
        {
            // exp within the 30s buffer => the entry is evicted on the next lookup and re-acquired.
            var jwt = CreateJwtWithExpiry(DateTimeOffset.UtcNow.AddSeconds(5));
            var (provider, urls) = CreateCapturingProvider(responseToken: jwt);

            await provider.GetAccessTokenAsync("https://graph.microsoft.com", new List<string> { "User.Read" });
            await provider.GetAccessTokenAsync("https://graph.microsoft.com", new List<string> { "User.Read" });

            Assert.Equal(2, urls.Count);
        }

        [Fact]
        public async Task GetAccessTokenAsync_ValidJwt_SecondCall_ServedFromCache()
        {
            var jwt = CreateJwtWithExpiry(DateTimeOffset.UtcNow.AddHours(1));
            var (provider, urls) = CreateCapturingProvider(responseToken: jwt);

            await provider.GetAccessTokenAsync("https://graph.microsoft.com", new List<string> { "User.Read" });
            await provider.GetAccessTokenAsync("https://graph.microsoft.com", new List<string> { "User.Read" });

            // exp is an hour out => well beyond the 30s buffer, so the second call is a cache hit.
            Assert.Single(urls);
        }

        [Fact]
        public async Task GetAgenticApplicationTokenAsync_SecondCall_ServedFromCache()
        {
            var (provider, urls) = CreateCapturingProvider(responseToken: "blueprint-token");

            await provider.GetAgenticApplicationTokenAsync("tenant-id", "agent-app-id");
            await provider.GetAgenticApplicationTokenAsync("tenant-id", "agent-app-id");

            Assert.Single(urls);
        }

        [Fact]
        public async Task GetAgenticUserTokenAsync_DifferentUsers_UseDistinctCacheEntries()
        {
            var (provider, urls) = CreateCapturingProvider("botframework", "user-token");

            await provider.GetAgenticUserTokenAsync("tenant-id", "agent-app-id", "alice@contoso.com", new List<string> { "User.Read" });
            await provider.GetAgenticUserTokenAsync("tenant-id", "agent-app-id", "bob@contoso.com", new List<string> { "User.Read" });

            // Different agentic users must never share a cached token.
            Assert.Equal(2, urls.Count);
        }
    }

    public class SidecarTokenCredentialTests
    {
        private static SidecarAuth CreateProvider(string responseToken)
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
            return new SidecarAuth(sidecarClient, new SidecarConnectionSettings { ServiceName = "default" });
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
            var concrete = sp.GetRequiredService<SidecarAuth>();

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
            var provider = sp.GetRequiredService<SidecarAuth>();

            // BlueprintServiceName must survive registration, else it falls back to "agenticblueprint".
            await provider.GetAgenticApplicationTokenAsync("tenant-1", "agent-1");
            Assert.Contains(capturedUrls, u => u.Contains("/AuthorizationHeaderUnauthenticated/myblueprint"));
            Assert.DoesNotContain(capturedUrls, u => u.Contains("agenticblueprint"));

            // Scopes must survive on the connection settings (used by the resource/instance token call).
            capturedUrls.Clear();
            await provider.GetAgenticInstanceTokenAsync("tenant-1", "agent-1");
            Assert.Contains(capturedUrls, u => u.Contains("optionsOverride.Scopes=Custom.Scope"));
        }

        [Fact]
        public async Task AddSidecarConnections_MissingServiceName_DefaultsToDefault()
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

            // ServiceName explicitly blank -> the registration guard must fall back to "default".
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                { "EntraSidecar:ServiceName", "" },
                { "EntraSidecar:Scopes:0", "Custom.Scope" }
            }).Build();

            var services = new ServiceCollection();
            services.AddSidecarConnections(configuration);
            services.AddHttpClient(SidecarHttpClient.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => mock.Object);

            var sp = services.BuildServiceProvider();
            var provider = sp.GetRequiredService<SidecarAuth>();

            await provider.GetAgenticInstanceTokenAsync("tenant-1", "agent-1");
            Assert.Contains(capturedUrls, u => u.Contains("/AuthorizationHeaderUnauthenticated/default"));
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

        [Fact]
        public void AddSidecarStartupProbe_RegistersHostedService_ReusingRegisteredClient()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new SidecarHttpClient(new HttpClient(), "http://localhost:5178"));
            services.AddSidecarStartupProbe();

            var sp = services.BuildServiceProvider();

            var hosted = Assert.Single(sp.GetServices<IHostedService>(), s => s is SidecarStartupHealthCheck);
            Assert.IsType<SidecarStartupHealthCheck>(hosted);
        }

        [Fact]
        public void AddSidecarStartupProbe_CreatesClient_WhenNoneRegistered()
        {
            var services = new ServiceCollection();
            services.AddSidecarStartupProbe(failOnUnreachable: true, sidecarBaseUrl: "http://localhost:5178", timeout: TimeSpan.FromSeconds(2));

            var sp = services.BuildServiceProvider();

            var hosted = Assert.Single(sp.GetServices<IHostedService>(), s => s is SidecarStartupHealthCheck);
            Assert.IsType<SidecarStartupHealthCheck>(hosted);
        }

        [Fact]
        public void AddSidecarStartupProbe_CreatesClient_UsingHttpClientFactory()
        {
            var services = new ServiceCollection();
            services.AddHttpClient(SidecarHttpClient.HttpClientName);
            services.AddSidecarStartupProbe(sidecarBaseUrl: "http://localhost:5178");

            var sp = services.BuildServiceProvider();

            var hosted = Assert.Single(sp.GetServices<IHostedService>(), s => s is SidecarStartupHealthCheck);
            Assert.IsType<SidecarStartupHealthCheck>(hosted);
        }
    }

    public class SidecarTokenExpiryTests
    {
        [Fact]
        public void Resolve_ValidJwt_ReturnsExpClaim()
        {
            var expiry = DateTimeOffset.UtcNow.AddHours(1);
            var token = new JwtSecurityToken(issuer: null, audience: null, claims: null,
                notBefore: null, expires: expiry.UtcDateTime);
            var jwt = new JwtSecurityTokenHandler().WriteToken(token);

            var resolved = SidecarTokenExpiry.Resolve(jwt);

            // JWT exp has whole-second resolution.
            Assert.InRange(resolved, expiry.AddSeconds(-2), expiry.AddSeconds(2));
        }

        [Fact]
        public void Resolve_OpaqueToken_UsesFallbackLifetime()
        {
            var before = DateTimeOffset.UtcNow;
            var resolved = SidecarTokenExpiry.Resolve("opaque-non-jwt-token");
            var after = DateTimeOffset.UtcNow;

            Assert.InRange(resolved, before.AddMinutes(5).AddSeconds(-5), after.AddMinutes(5).AddSeconds(5));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Resolve_NullOrEmpty_UsesFallbackLifetime(string token)
        {
            var before = DateTimeOffset.UtcNow;
            var resolved = SidecarTokenExpiry.Resolve(token);
            var after = DateTimeOffset.UtcNow;

            Assert.InRange(resolved, before.AddMinutes(5).AddSeconds(-5), after.AddMinutes(5).AddSeconds(5));
        }

        [Fact]
        public void Resolve_JwtWithoutExpClaim_UsesFallbackLifetime()
        {
            // A readable JWT that carries no "exp" claim -> ValidTo is DateTime.MinValue -> fallback.
            var token = new JwtSecurityToken(issuer: "iss", audience: "aud", claims: null,
                notBefore: null, expires: null);
            var jwt = new JwtSecurityTokenHandler().WriteToken(token);

            var before = DateTimeOffset.UtcNow;
            var resolved = SidecarTokenExpiry.Resolve(jwt);
            var after = DateTimeOffset.UtcNow;

            Assert.InRange(resolved, before.AddMinutes(5).AddSeconds(-5), after.AddMinutes(5).AddSeconds(5));
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
            Assert.Equal(TimeSpan.FromSeconds(30), settings.RequestTimeout);
            Assert.Equal(3, settings.RetryCount);
        }

        [Fact]
        public void ConfigConstructor_BindsTimeoutAndRetryCount()
        {
            var section = Section(new Dictionary<string, string>
            {
                { "Conn:RequestTimeout", "00:00:45" },
                { "Conn:RetryCount", "5" },
                { "Conn:SidecarBaseUrl", "http://localhost:9999" }
            });

            var settings = new SidecarConnectionSettings(section);

            Assert.Equal(TimeSpan.FromSeconds(45), settings.RequestTimeout);
            Assert.Equal(5, settings.RetryCount);
            Assert.Equal("http://localhost:9999", settings.SidecarBaseUrl);
        }

        [Fact]
        public void Bind_PopulatesTimeoutAndRetryCount()
        {
            var section = Section(new Dictionary<string, string>
            {
                { "Conn:RequestTimeout", "00:01:00" },
                { "Conn:RetryCount", "7" }
            });

            var settings = section.Get<SidecarConnectionSettings>();

            Assert.Equal(TimeSpan.FromMinutes(1), settings.RequestTimeout);
            Assert.Equal(7, settings.RetryCount);
        }

        [Fact]
        public void ConfigConstructor_NonExistentSection_Throws()
        {
            var section = new ConfigurationBuilder().Build().GetSection("Missing");
            Assert.Throws<ArgumentException>(() => new SidecarConnectionSettings(section));
        }
    }

    public class SidecarBaseUrlValidationTests
    {
        [Theory]
        [InlineData("http://localhost:5178")]
        [InlineData("http://127.0.0.1:5178")]
        [InlineData("http://[::1]:5178")]
        [InlineData("http://10.0.0.5:8080")]
        [InlineData("http://172.16.4.3:8080")]
        [InlineData("http://172.31.255.1:8080")]
        [InlineData("http://192.168.1.10:8080")]
        [InlineData("http://169.254.10.10:8080")]
        [InlineData("http://[fc00::1]:8080")]
        [InlineData("http://[fd12:3456:789a::1]:8080")]
        [InlineData("http://[fe80::1]:8080")]
        [InlineData("http://agentid-sidecar.localhost:8080")]
        public void ValidateBaseUrl_AllowsLoopbackAndPrivate(string url)
        {
            // Should not throw for loopback / private addresses without the bypass flag.
            SidecarHttpClient.ValidateBaseUrl(url, bypassLocalNetworkRestriction: false);
        }

        [Theory]
        [InlineData("http://20.50.60.70:8080")]
        [InlineData("http://8.8.8.8")]
        [InlineData("http://[2001:4860:4860::8888]")]
        [InlineData("https://sidecar.contoso.com")]
        [InlineData("http://172.32.0.1:8080")]
        public void ValidateBaseUrl_RejectsPublicAddress_WhenNotBypassed(string url)
        {
            Assert.Throws<InvalidOperationException>(
                () => SidecarHttpClient.ValidateBaseUrl(url, bypassLocalNetworkRestriction: false));
        }

        [Theory]
        [InlineData("http://20.50.60.70:8080")]
        [InlineData("https://sidecar.contoso.com")]
        public void ValidateBaseUrl_AllowsPublicAddress_WhenBypassEnabled(string url)
        {
            // The explicit (unsafe) bypass flag skips the safety check.
            SidecarHttpClient.ValidateBaseUrl(url, bypassLocalNetworkRestriction: true);
        }

        [Fact]
        public void ValidateBaseUrl_PublicSidecarUrlEnvVar_IsRejected_WhenConfigIsSafeLocalhost()
        {
            // Repro for the SSRF env-var bypass: a safe localhost config must NOT let a public
            // SIDECAR_URL env value through, because validation no longer keys off config presence.
            var original = Environment.GetEnvironmentVariable("SIDECAR_URL");
            try
            {
                Environment.SetEnvironmentVariable("SIDECAR_URL", "https://attacker.contoso.com");
                var resolved = SidecarHttpClient.ResolveBaseUrl(configuredUrl: "http://localhost:5178");

                Assert.Equal("https://attacker.contoso.com", resolved);
                Assert.Throws<InvalidOperationException>(
                    () => SidecarHttpClient.ValidateBaseUrl(resolved, bypassLocalNetworkRestriction: false));

                // The operator can still opt in explicitly (unsafe) via the bypass flag.
                SidecarHttpClient.ValidateBaseUrl(resolved, bypassLocalNetworkRestriction: true);
            }
            finally
            {
                Environment.SetEnvironmentVariable("SIDECAR_URL", original);
            }
        }

        [Fact]
        public void ValidateBaseUrl_MalformedUrl_Throws()
        {
            Assert.Throws<InvalidOperationException>(
                () => SidecarHttpClient.ValidateBaseUrl("not-a-url", bypassLocalNetworkRestriction: false));
        }

        [Fact]
        public void ValidateBaseUrl_MalformedUrl_Throws_EvenWhenBypassed()
        {
            // The bypass flag only relaxes the loopback/private-address rule, never basic URL validation.
            Assert.Throws<InvalidOperationException>(
                () => SidecarHttpClient.ValidateBaseUrl("not-a-url", bypassLocalNetworkRestriction: true));
        }

        [Theory]
        [InlineData("file:///etc/passwd")]
        [InlineData("ftp://localhost/secret")]
        [InlineData("gopher://localhost:70")]
        public void ValidateBaseUrl_RejectsNonHttpScheme(string url)
        {
            // Non-HTTP(S) schemes must be rejected regardless of the bypass flag.
            Assert.Throws<InvalidOperationException>(
                () => SidecarHttpClient.ValidateBaseUrl(url, bypassLocalNetworkRestriction: false));
            Assert.Throws<InvalidOperationException>(
                () => SidecarHttpClient.ValidateBaseUrl(url, bypassLocalNetworkRestriction: true));
        }

        [Theory]
        [InlineData("http://user:pass@localhost:5178")]
        [InlineData("http://user@localhost:5178")]
        public void ValidateBaseUrl_RejectsUserInfo(string url)
        {
            // Embedded credentials must be rejected regardless of the bypass flag.
            Assert.Throws<InvalidOperationException>(
                () => SidecarHttpClient.ValidateBaseUrl(url, bypassLocalNetworkRestriction: false));
            Assert.Throws<InvalidOperationException>(
                () => SidecarHttpClient.ValidateBaseUrl(url, bypassLocalNetworkRestriction: true));
        }
    }

    public class SidecarHttpClientRetryTests
    {
        private static readonly TimeSpan FastBackoff = TimeSpan.FromMilliseconds(1);

        private static Mock<HttpMessageHandler> SequenceHandler(params HttpResponseMessage[] responses)
        {
            var mock = new Mock<HttpMessageHandler>();
            var setup = mock.Protected()
                .SetupSequence<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
            foreach (var response in responses)
            {
                setup = setup.ReturnsAsync(response);
            }

            return mock;
        }

        private static HttpResponseMessage Ok(string token = "Bearer eyJok")
            => new() { StatusCode = HttpStatusCode.OK, Content = new StringContent($$"""{"authorizationHeader":"{{token}}"}""") };

        private static HttpResponseMessage Status(HttpStatusCode code)
            => new() { StatusCode = code, Content = new StringContent("") };

        private static void VerifyCallCount(Mock<HttpMessageHandler> mock, int times)
        {
            mock.Protected().Verify(
                "SendAsync",
                Times.Exactly(times),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task RetriesTransientStatus_ThenSucceeds()
        {
            var handler = SequenceHandler(Status(HttpStatusCode.ServiceUnavailable), Status(HttpStatusCode.InternalServerError), Ok());
            var client = new SidecarHttpClient(new HttpClient(handler.Object), "http://localhost:5178", null, null, retryCount: 3, retryBackoffBase: FastBackoff);

            var result = await client.GetAuthorizationHeaderUnauthenticatedAsync("default");

            Assert.Equal("eyJok", result.Token);
            VerifyCallCount(handler, 3);
        }

        [Fact]
        public async Task GivesUpAfterRetryCount_ThrowsWithStatus()
        {
            var handler = SequenceHandler(Status(HttpStatusCode.InternalServerError), Status(HttpStatusCode.InternalServerError));
            var client = new SidecarHttpClient(new HttpClient(handler.Object), "http://localhost:5178", null, null, retryCount: 1, retryBackoffBase: FastBackoff);

            var ex = await Assert.ThrowsAsync<ErrorResponseException>(
                () => client.GetAuthorizationHeaderUnauthenticatedAsync("default"));

            Assert.Equal(500, ex.StatusCode);
            VerifyCallCount(handler, 2);
        }

        [Fact]
        public async Task DoesNotRetryNonTransientStatus()
        {
            var handler = SequenceHandler(Status(HttpStatusCode.BadRequest), Ok());
            var client = new SidecarHttpClient(new HttpClient(handler.Object), "http://localhost:5178", null, null, retryCount: 3, retryBackoffBase: FastBackoff);

            await Assert.ThrowsAsync<ErrorResponseException>(
                () => client.GetAuthorizationHeaderUnauthenticatedAsync("default"));

            VerifyCallCount(handler, 1);
        }

        [Fact]
        public async Task RetriesOnNetworkException_ThenSucceeds()
        {
            var mock = new Mock<HttpMessageHandler>();
            mock.Protected()
                .SetupSequence<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("connection refused"))
                .ReturnsAsync(Ok());
            var client = new SidecarHttpClient(new HttpClient(mock.Object), "http://localhost:5178", null, null, retryCount: 2, retryBackoffBase: FastBackoff);

            var result = await client.GetAuthorizationHeaderUnauthenticatedAsync("default");

            Assert.Equal("eyJok", result.Token);
            VerifyCallCount(mock, 2);
        }

        [Fact]
        public async Task RetryDisabled_WhenRetryCountZero()
        {
            var handler = SequenceHandler(Status(HttpStatusCode.ServiceUnavailable), Ok());
            var client = new SidecarHttpClient(new HttpClient(handler.Object), "http://localhost:5178", null, null, retryCount: 0, retryBackoffBase: FastBackoff);

            await Assert.ThrowsAsync<ErrorResponseException>(
                () => client.GetAuthorizationHeaderUnauthenticatedAsync("default"));

            VerifyCallCount(handler, 1);
        }
    }
}
