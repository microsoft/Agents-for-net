// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

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
    [Fact]
    public async Task SendAsync_AuthenticatedMode_AddsBearerHeader()
    {
        var session = new A2AAuthenticationSession { Mode = A2AAuthMode.Delegated };
        var tokens = new Mock<IA2AAccessTokenProvider>();
        tokens.Setup(provider => provider.GetAccessTokenAsync(A2AAuthMode.Delegated, It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-token");
        var recorder = new RecordingHttpMessageHandler();
        using var client = new HttpClient(new AuthenticatedA2AHttpHandler(session, tokens.Object)
        {
            InnerHandler = recorder,
        });

        await client.GetAsync("https://agent.example/.well-known/agent-card.json");

        Assert.Equal("Bearer", recorder.Request!.Headers.Authorization!.Scheme);
        Assert.Equal("test-token", recorder.Request.Headers.Authorization.Parameter);
        tokens.Verify(provider => provider.GetAccessTokenAsync(A2AAuthMode.Delegated, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_NoneMode_DoesNotAddBearerHeader()
    {
        var session = new A2AAuthenticationSession { Mode = A2AAuthMode.None };
        var tokens = new Mock<IA2AAccessTokenProvider>(MockBehavior.Strict);
        tokens.Setup(provider => provider.GetAccessTokenAsync(A2AAuthMode.None, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var recorder = new RecordingHttpMessageHandler();
        using var client = new HttpClient(new AuthenticatedA2AHttpHandler(session, tokens.Object)
        {
            InnerHandler = recorder,
        });

        await client.GetAsync("https://agent.example/.well-known/agent-card.json");

        Assert.Null(recorder.Request!.Headers.Authorization);
        tokens.Verify(provider => provider.GetAccessTokenAsync(A2AAuthMode.None, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_WithExistingAuthorization_ReplacesItWithBearerToken()
    {
        var session = new A2AAuthenticationSession { Mode = A2AAuthMode.App };
        var tokens = new Mock<IA2AAccessTokenProvider>();
        tokens.Setup(provider => provider.GetAccessTokenAsync(A2AAuthMode.App, It.IsAny<CancellationToken>()))
            .ReturnsAsync("app-token");
        var recorder = new RecordingHttpMessageHandler();
        using var client = new HttpClient(new AuthenticatedA2AHttpHandler(session, tokens.Object)
        {
            InnerHandler = recorder,
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://agent.example/.well-known/agent-card.json");
        request.Headers.TryAddWithoutValidation("Authorization", "Basic stale");

        await client.SendAsync(request);

        Assert.Equal("Bearer", recorder.Request!.Headers.Authorization!.Scheme);
        Assert.Equal("app-token", recorder.Request.Headers.Authorization.Parameter);
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
