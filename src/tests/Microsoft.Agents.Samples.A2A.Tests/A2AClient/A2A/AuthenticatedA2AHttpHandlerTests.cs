// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using A2A;
using Microsoft.Agents.Samples.A2AClient.A2A;
using Moq;
using Xunit;

namespace Microsoft.Agents.Samples.A2AClient.Tests.A2A;

public class AuthenticatedA2AHttpHandlerTests
{
    private static readonly Uri AgentUrl = new("https://agent.example/a2a");

    [Fact]
    public async Task SendAsync_AuthenticatedMode_AddsBearerHeader()
    {
        A2AAgentCardAuthentication authentication = CreateGitHubAuthentication();
        var tokens = CreateTokenProvider(authentication, "test-token");
        var recorder = new RecordingHttpMessageHandler();
        using var client = CreateClient(authentication, tokens.Object, recorder);

        await client.GetAsync("https://agent.example/.well-known/agent-card.json");

        Assert.Equal("Bearer", recorder.Request!.Headers.Authorization!.Scheme);
        Assert.Equal("test-token", recorder.Request.Headers.Authorization.Parameter);
        tokens.Verify(provider => provider.GetAccessTokenAsync(authentication, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_NoneMode_DoesNotAddBearerHeader()
    {
        var tokens = new Mock<IA2AAccessTokenProvider>(MockBehavior.Strict);
        tokens.Setup(provider => provider.GetAccessTokenAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var recorder = new RecordingHttpMessageHandler();
        using var client = CreateClient(authentication: null, tokens.Object, recorder);

        await client.GetAsync("https://agent.example/.well-known/agent-card.json");

        Assert.Null(recorder.Request!.Headers.Authorization);
        tokens.Verify(provider => provider.GetAccessTokenAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_WithExistingAuthorization_ReplacesItWithBearerToken()
    {
        A2AAgentCardAuthentication authentication = CreateGitHubAuthentication();
        var tokens = CreateTokenProvider(authentication, "app-token");
        var recorder = new RecordingHttpMessageHandler();
        using var client = CreateClient(authentication, tokens.Object, recorder);

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://agent.example/.well-known/agent-card.json");
        request.Headers.TryAddWithoutValidation("Authorization", "Basic stale");

        await client.SendAsync(request);

        Assert.Equal("Bearer", recorder.Request!.Headers.Authorization!.Scheme);
        Assert.Equal("app-token", recorder.Request.Headers.Authorization.Parameter);
    }

    [Theory]
    [InlineData("https://attacker.example/a2a")]
    [InlineData("https://agent.example:8443/a2a")]
    [InlineData("http://agent.example/a2a")]
    public async Task SendAsync_CrossOriginTarget_FailsWithoutSendingToken(string target)
    {
        A2AAgentCardAuthentication authentication = CreateGitHubAuthentication();
        var tokens = CreateTokenProvider(authentication, "test-token");
        var recorder = new RecordingHttpMessageHandler();
        using var client = CreateClient(authentication, tokens.Object, recorder);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAsync(target));

        Assert.Null(recorder.Request);
        Assert.DoesNotContain("test-token", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_CrossOriginTargetInNoneMode_IsNotBlocked()
    {
        var tokens = new Mock<IA2AAccessTokenProvider>();
        tokens.Setup(provider => provider.GetAccessTokenAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var recorder = new RecordingHttpMessageHandler();
        using var client = CreateClient(authentication: null, tokens.Object, recorder);

        await client.GetAsync("https://attacker.example/a2a");

        Assert.NotNull(recorder.Request);
        Assert.Null(recorder.Request!.Headers.Authorization);
    }

    [Fact]
    public async Task SendAsync_PlaintextNonLoopbackAgent_FailsWithoutSendingToken()
    {
        A2AAgentCardAuthentication authentication = CreateGitHubAuthentication();
        var tokens = CreateTokenProvider(authentication, "app-token");
        var recorder = new RecordingHttpMessageHandler();
        var session = new A2AAuthenticationSession();
        session.SetAutomaticAuthentication(authentication);
        using var client = new HttpClient(new AuthenticatedA2AHttpHandler(
            session,
            tokens.Object,
            new Uri("http://agent.example/a2a"),
            recorder));

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAsync("http://agent.example/a2a"));

        Assert.Null(recorder.Request);
    }

    [Fact]
    public async Task SendAsync_LoopbackHttpAgent_AttachesToken()
    {
        A2AAgentCardAuthentication authentication = CreateGitHubAuthentication();
        var tokens = CreateTokenProvider(authentication, "local-token");
        var recorder = new RecordingHttpMessageHandler();
        var session = new A2AAuthenticationSession();
        session.SetAutomaticAuthentication(authentication);
        using var client = new HttpClient(new AuthenticatedA2AHttpHandler(
            session,
            tokens.Object,
            new Uri("http://localhost:3978/a2a"),
            recorder));

        await client.GetAsync("http://localhost:3978/a2a");

        Assert.Equal("local-token", recorder.Request!.Headers.Authorization!.Parameter);
    }

    [Fact]
    public void CreateInnerHandler_DisablesAutomaticRedirects()
    {
        // Redirects are followed below the delegating handler, so an enabled redirect would carry the
        // Authorization header to a target the origin check never saw.
        using var handler = Assert.IsType<HttpClientHandler>(AuthenticatedA2AHttpHandler.CreateInnerHandler());

        Assert.False(handler.AllowAutoRedirect);
    }

    private static Mock<IA2AAccessTokenProvider> CreateTokenProvider(A2AAgentCardAuthentication? authentication, string token)
    {
        var tokens = new Mock<IA2AAccessTokenProvider>();
        tokens.Setup(provider => provider.GetAccessTokenAsync(authentication, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        return tokens;
    }

    private static HttpClient CreateClient(A2AAgentCardAuthentication? authentication, IA2AAccessTokenProvider tokens, HttpMessageHandler inner)
    {
        var session = new A2AAuthenticationSession();
        session.SetAutomaticAuthentication(authentication);
        return new HttpClient(new AuthenticatedA2AHttpHandler(session, tokens, AgentUrl, inner));
    }

    private static AgentCard CreateTwoProviderCard()
    {
        return new AgentCard
        {
            SecuritySchemes = new Dictionary<string, SecurityScheme>
            {
                ["delegated"] = new()
                {
                    OAuth2SecurityScheme = new OAuth2SecurityScheme
                    {
                        Flows = new OAuthFlows
                        {
                            DeviceCode = new()
                            {
                                DeviceAuthorizationUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode",
                                TokenUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/token",
                            },
                        },
                    },
                },
                ["github"] = new()
                {
                    OAuth2SecurityScheme = new OAuth2SecurityScheme
                    {
                        Flows = new OAuthFlows
                        {
                            DeviceCode = new()
                            {
                                DeviceAuthorizationUrl = "https://github.com/login/device/code",
                                TokenUrl = "https://github.com/login/oauth/access_token",
                            },
                        },
                    },
                },
            },
            Skills =
            [
                new AgentSkill
                {
                    Id = "Microsoft Graph profile",
                    Name = "Microsoft Graph profile",
                    Examples = ["-me"],
                    SecurityRequirements =
                    [
                        new SecurityRequirement
                        {
                            Schemes = new Dictionary<string, StringList>
                            {
                                ["delegated"] = new() { List = ["api://agent/access_as_user"] },
                            },
                        },
                    ],
                },
                new AgentSkill
                {
                    Id = "GitHub assigned issues",
                    Name = "GitHub assigned issues",
                    Examples = ["-issues"],
                    SecurityRequirements =
                    [
                        new SecurityRequirement
                        {
                            Schemes = new Dictionary<string, StringList>
                            {
                                ["github"] = new() { List = ["repo"] },
                            },
                        },
                    ],
                },
            ],
        };
    }

    private static A2AAgentCardAuthentication CreateGitHubAuthentication()
    {
        AgentCard card = CreateTwoProviderCard();
        AgentSkill skill = card.Skills![1];
        return A2AAgentCardAuthentication.Select(card, skill, A2AAuthMode.Delegated)!;
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
