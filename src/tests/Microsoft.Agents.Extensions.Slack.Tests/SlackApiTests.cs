// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using Microsoft.Agents.Extensions.Slack.Api;
using Moq;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.Slack.Tests;

public class SlackApiTests
{
    [Fact]
    public void Constructor_ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => new SlackApi(null!));
    }

    [Theory]
    [InlineData(null, typeof(ArgumentNullException))]
    [InlineData("", typeof(ArgumentException))]
    [InlineData("   ", typeof(ArgumentException))]
    public async Task CallAsync_ThrowsOnNullMethod(string? method, Type exceptionType)
    {
        var slackApi = CreateSlackApi((_, _) => Task.FromResult(CreateJsonResponse("""{"ok":true}""")));

        var exception = await Record.ExceptionAsync(() => slackApi.CallAsync(method!));

        Assert.NotNull(exception);
        Assert.IsType(exceptionType, exception);
    }

    [Fact]
    public async Task CallAsync_Success_ReturnsSlackResponse()
    {
        var factory = CreateFactory((request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://slack.com/api/chat.postMessage", request.RequestUri!.ToString());

            return Task.FromResult(CreateJsonResponse("""{"ok":true,"ts":"123"}"""));
        });
        var slackApi = new SlackApi(factory.Object);

        var response = await slackApi.CallAsync("chat.postMessage");

        Assert.True(response.ok);
        Assert.Equal("123", response.ts);
        factory.Verify(f => f.CreateClient(nameof(SlackApi)), Times.Once);
    }

    [Fact]
    public async Task CallAsync_SetsAuthorizationHeader_WhenTokenProvided()
    {
        AuthenticationHeaderValue? authorization = null;
        var slackApi = CreateSlackApi((request, _) =>
        {
            authorization = request.Headers.Authorization;
            return Task.FromResult(CreateJsonResponse("""{"ok":true}"""));
        });

        await slackApi.CallAsync("auth.test", token: "xoxb-token");

        Assert.NotNull(authorization);
        Assert.Equal("Bearer", authorization!.Scheme);
        Assert.Equal("xoxb-token", authorization.Parameter);
    }

    [Fact]
    public async Task CallAsync_NoAuthHeader_WhenTokenEmpty()
    {
        AuthenticationHeaderValue? authorization = null;
        var slackApi = CreateSlackApi((request, _) =>
        {
            authorization = request.Headers.Authorization;
            return Task.FromResult(CreateJsonResponse("""{"ok":true}"""));
        });

        await slackApi.CallAsync("auth.test", token: "");

        Assert.Null(authorization);
    }

    [Fact]
    public async Task CallAsync_SerializesOptionsAsJson()
    {
        string? body = null;
        string? mediaType = null;
        var slackApi = CreateSlackApi(async (request, cancellationToken) =>
        {
            body = await request.Content!.ReadAsStringAsync(cancellationToken);
            mediaType = request.Content.Headers.ContentType?.MediaType;
            return CreateJsonResponse("""{"ok":true}""");
        });

        await slackApi.CallAsync("chat.postMessage", new { channel = "C123", text = "hello", thread_ts = (string?)null });

        Assert.Equal("""{"channel":"C123","text":"hello"}""", body);
        Assert.Equal("application/json", mediaType);
    }

    [Fact]
    public async Task CallAsync_PassesStringOptionsDirectly()
    {
        const string payload = """{"channel":"C123","text":"hello"}""";
        string? body = null;
        var slackApi = CreateSlackApi(async (request, cancellationToken) =>
        {
            body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return CreateJsonResponse("""{"ok":true}""");
        });

        await slackApi.CallAsync("chat.postMessage", payload);

        Assert.Equal(payload, body);
    }

    [Fact]
    public async Task CallAsync_ThrowsSlackResponseException_OnApiError()
    {
        var slackApi = CreateSlackApi((_, _) => Task.FromResult(CreateJsonResponse("""{"ok":false,"error":"channel_not_found"}""")));

        var exception = await Assert.ThrowsAsync<SlackResponseException>(() => slackApi.CallAsync("conversations.info"));

        Assert.Contains("Slack API error on conversations.info (HTTP 200)", exception.Message, StringComparison.Ordinal);
        Assert.Contains("channel_not_found", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallAsync_ThrowsSlackResponseException_OnHttpError()
    {
        var slackApi = CreateSlackApi((_, _) =>
            Task.FromResult(CreateJsonResponse("""{"ok":false,"error":"internal_error"}""", HttpStatusCode.InternalServerError)));

        var exception = await Assert.ThrowsAsync<SlackResponseException>(() => slackApi.CallAsync("chat.postMessage"));

        Assert.Contains("Slack API error on chat.postMessage (HTTP 500)", exception.Message, StringComparison.Ordinal);
        Assert.Contains("internal_error", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallAsync_ThrowsSlackResponseException_OnInvalidJson()
    {
        var slackApi = CreateSlackApi((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("not-json", Encoding.UTF8, "text/plain")
            }));

        var exception = await Assert.ThrowsAsync<SlackResponseException>(() => slackApi.CallAsync("chat.postMessage"));

        Assert.Contains("Slack API error on chat.postMessage (HTTP 200)", exception.Message, StringComparison.Ordinal);
        Assert.Contains("not-json", exception.Message, StringComparison.Ordinal);
    }

    private static SlackApi CreateSlackApi(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendFunc)
        => new(CreateFactory(sendFunc).Object);

    private static Mock<IHttpClientFactory> CreateFactory(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendFunc)
    {
        var handler = new TestHandler(sendFunc);
        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(nameof(SlackApi)))
            .Returns(httpClient);

        return factory;
    }

    private static HttpResponseMessage CreateJsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
        => new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private class TestHandler : DelegatingHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendFunc;

        public TestHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendFunc)
        {
            _sendFunc = sendFunc;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _sendFunc(request, cancellationToken);
    }
}
