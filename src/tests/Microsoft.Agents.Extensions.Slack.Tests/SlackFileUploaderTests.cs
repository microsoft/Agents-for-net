// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using Microsoft.Agents.Extensions.Slack.Api;
using Moq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.Slack.Tests;

public class SlackFileUploaderTests
{
    [Fact]
    public async Task UploadAsync_UsesExternalUploadSequenceAndReturnsPrivateUrl()
    {
        var requests = new List<RecordedRequest>();
        var responses = new Queue<HttpResponseMessage>(
        [
            CreateJsonResponse(
                """{"ok":true,"upload_url":"https://uploads.slack.test/file?signature=secret","file_id":"F123"}"""),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty)
            },
            CreateJsonResponse(
                """{"ok":true,"files":[{"id":"F123","url_private":"https://files.slack.test/private","permalink":"https://slack.test/permalink"}]}"""),
        ]);
        var uploader = CreateUploader(async (request, cancellationToken) =>
        {
            requests.Add(await RecordedRequest.CreateAsync(request, cancellationToken));
            return responses.Dequeue();
        });

        var result = await uploader.UploadAsync(
            [0, 1, 2, 255],
            "report.pdf",
            "C123",
            "xoxb-token",
            CancellationToken.None);

        Assert.Equal("https://files.slack.test/private", result);
        Assert.Collection(
            requests,
            request =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("https://slack.com/api/files.getUploadURLExternal", request.Uri);
                Assert.Equal("""{"filename":"report.pdf","length":4}""", request.TextBody);
                AssertBearerToken(request.Authorization, "xoxb-token");
            },
            request =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("https://uploads.slack.test/file?signature=secret", request.Uri);
                Assert.Equal(new byte[] { 0, 1, 2, 255 }, request.Body);
                Assert.Null(request.Authorization);
            },
            request =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("https://slack.com/api/files.completeUploadExternal", request.Uri);
                Assert.Equal(
                    """{"files":[{"id":"F123","title":"report.pdf"}],"channel_id":"C123"}""",
                    request.TextBody);
                AssertBearerToken(request.Authorization, "xoxb-token");
            });
    }

    [Fact]
    public async Task UploadAsync_ReturnsPermalinkWhenPrivateUrlMissing()
    {
        var responses = new Queue<HttpResponseMessage>(
        [
            CreateJsonResponse(
                """{"ok":true,"upload_url":"https://uploads.slack.test/file","file_id":"F123"}"""),
            new HttpResponseMessage(HttpStatusCode.OK),
            CreateJsonResponse(
                """{"ok":true,"files":[{"id":"F123","permalink":"https://slack.test/permalink"}]}"""),
        ]);
        var uploader = CreateUploader((_, _) => Task.FromResult(responses.Dequeue()));

        var result = await uploader.UploadAsync(
            [1],
            "report.txt",
            "C123",
            "xoxb-token",
            CancellationToken.None);

        Assert.Equal("https://slack.test/permalink", result);
    }

    [Theory]
    [InlineData("""{"ok":true,"file_id":"F123"}""")]
    [InlineData("""{"ok":true,"upload_url":"https://uploads.slack.test/file"}""")]
    public async Task UploadAsync_MissingUploadTarget_ThrowsSlackResponseException(string responseBody)
    {
        var uploader = CreateUploader((_, _) =>
            Task.FromResult(CreateJsonResponse(responseBody)));

        var exception = await Assert.ThrowsAsync<SlackResponseException>(() =>
            uploader.UploadAsync(
                [1],
                "report.txt",
                "C123",
                "xoxb-token",
                CancellationToken.None));

        Assert.Contains("files.getUploadURLExternal", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadAsync_CancellationDuringRawUpload_PropagatesWithoutCompleting()
    {
        using var cancellation = new CancellationTokenSource();
        var requestCount = 0;
        var uploader = CreateUploader((_, cancellationToken) =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                return Task.FromResult(CreateJsonResponse(
                    """{"ok":true,"upload_url":"https://uploads.slack.test/file","file_id":"F123"}"""));
            }

            cancellation.Cancel();
            return Task.FromCanceled<HttpResponseMessage>(cancellationToken);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            uploader.UploadAsync(
                [1, 2, 3],
                "report.txt",
                "C123",
                "xoxb-token",
                cancellation.Token));

        Assert.Equal(2, requestCount);
    }

    [Fact]
    public async Task UploadAsync_FailedRawUpload_ThrowsWithoutCompleting()
    {
        var requestCount = 0;
        var uploader = CreateUploader((_, _) =>
        {
            requestCount++;
            return Task.FromResult(requestCount == 1
                ? CreateJsonResponse(
                    """{"ok":true,"upload_url":"https://uploads.slack.test/file","file_id":"F123"}""")
                : new HttpResponseMessage(HttpStatusCode.Forbidden)
                {
                    Content = new StringContent("signature rejected")
                });
        });

        var exception = await Assert.ThrowsAsync<SlackResponseException>(() =>
            uploader.UploadAsync(
                [1, 2, 3],
                "report.txt",
                "C123",
                "xoxb-token",
                CancellationToken.None));

        Assert.Equal(2, requestCount);
        Assert.Contains("HTTP 403", exception.Message, StringComparison.Ordinal);
        Assert.Contains("signature rejected", exception.Message, StringComparison.Ordinal);
    }

    private static SlackFileUploader CreateUploader(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendFunc)
    {
        var handler = new TestHandler(sendFunc);
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(nameof(SlackApi)))
            .Returns(() => new HttpClient(handler, disposeHandler: false));

        return new SlackFileUploader(new SlackApi(factory.Object));
    }

    private static HttpResponseMessage CreateJsonResponse(
        string json,
        HttpStatusCode statusCode = HttpStatusCode.OK)
        => new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private static void AssertBearerToken(AuthenticationHeaderValue? authorization, string token)
    {
        Assert.NotNull(authorization);
        Assert.Equal("Bearer", authorization!.Scheme);
        Assert.Equal(token, authorization.Parameter);
    }

    private sealed record RecordedRequest(
        HttpMethod Method,
        string Uri,
        AuthenticationHeaderValue? Authorization,
        byte[] Body)
    {
        internal string TextBody => Encoding.UTF8.GetString(Body);

        internal static async Task<RecordedRequest> CreateAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => new(
                request.Method,
                request.RequestUri!.ToString(),
                request.Headers.Authorization,
                request.Content == null
                    ? []
                    : await request.Content.ReadAsByteArrayAsync(cancellationToken));
    }

    private sealed class TestHandler : DelegatingHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendFunc;

        internal TestHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendFunc)
        {
            _sendFunc = sendFunc;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => _sendFunc(request, cancellationToken);
    }
}
