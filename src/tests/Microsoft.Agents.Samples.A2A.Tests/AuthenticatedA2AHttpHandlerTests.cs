// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Samples.A2AClient;
using Moq;
using Xunit;

namespace Microsoft.Agents.Samples.A2A.Tests;

public class AuthenticatedA2AHttpHandlerTests
{
    private static readonly Uri AgentUrl = new("https://agent.example/a2a");

    [Fact]
    public async Task SendAsync_AuthenticatedMode_AddsBearerHeader()
    {
        var tokens = CreateTokenProvider(A2AAuthMode.Delegated, "test-token");
        var recorder = new RecordingHttpMessageHandler();
        using var client = CreateClient(A2AAuthMode.Delegated, tokens.Object, recorder);

        await client.GetAsync("https://agent.example/.well-known/agent-card.json");

        Assert.Equal("Bearer", recorder.Request!.Headers.Authorization!.Scheme);
        Assert.Equal("test-token", recorder.Request.Headers.Authorization.Parameter);
        tokens.Verify(provider => provider.GetAccessTokenAsync(A2AAuthMode.Delegated, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_NoneMode_DoesNotAddBearerHeader()
    {
        var tokens = new Mock<IA2AAccessTokenProvider>(MockBehavior.Strict);
        tokens.Setup(provider => provider.GetAccessTokenAsync(A2AAuthMode.None, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var recorder = new RecordingHttpMessageHandler();
        using var client = CreateClient(A2AAuthMode.None, tokens.Object, recorder);

        await client.GetAsync("https://agent.example/.well-known/agent-card.json");

        Assert.Null(recorder.Request!.Headers.Authorization);
        tokens.Verify(provider => provider.GetAccessTokenAsync(A2AAuthMode.None, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_WithExistingAuthorization_ReplacesItWithBearerToken()
    {
        var tokens = CreateTokenProvider(A2AAuthMode.App, "app-token");
        var recorder = new RecordingHttpMessageHandler();
        using var client = CreateClient(A2AAuthMode.App, tokens.Object, recorder);

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
        var tokens = CreateTokenProvider(A2AAuthMode.Delegated, "test-token");
        var recorder = new RecordingHttpMessageHandler();
        using var client = CreateClient(A2AAuthMode.Delegated, tokens.Object, recorder);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAsync(target));

        Assert.Null(recorder.Request);
        Assert.DoesNotContain("test-token", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_CrossOriginTargetInNoneMode_IsNotBlocked()
    {
        var tokens = new Mock<IA2AAccessTokenProvider>();
        tokens.Setup(provider => provider.GetAccessTokenAsync(A2AAuthMode.None, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var recorder = new RecordingHttpMessageHandler();
        using var client = CreateClient(A2AAuthMode.None, tokens.Object, recorder);

        await client.GetAsync("https://attacker.example/a2a");

        Assert.NotNull(recorder.Request);
        Assert.Null(recorder.Request!.Headers.Authorization);
    }

    [Fact]
    public async Task SendAsync_PlaintextNonLoopbackAgent_FailsWithoutSendingToken()
    {
        var tokens = CreateTokenProvider(A2AAuthMode.App, "app-token");
        var recorder = new RecordingHttpMessageHandler();
        var session = new A2AAuthenticationSession { Mode = A2AAuthMode.App };
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
        var tokens = CreateTokenProvider(A2AAuthMode.Delegated, "local-token");
        var recorder = new RecordingHttpMessageHandler();
        var session = new A2AAuthenticationSession { Mode = A2AAuthMode.Delegated };
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

    private static Mock<IA2AAccessTokenProvider> CreateTokenProvider(A2AAuthMode mode, string token)
    {
        var tokens = new Mock<IA2AAccessTokenProvider>();
        tokens.Setup(provider => provider.GetAccessTokenAsync(mode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        return tokens;
    }

    private static HttpClient CreateClient(A2AAuthMode mode, IA2AAccessTokenProvider tokens, HttpMessageHandler inner)
    {
        var session = new A2AAuthenticationSession { Mode = mode };
        return new HttpClient(new AuthenticatedA2AHttpHandler(session, tokens, AgentUrl, inner));
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
