// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Slack.Api;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.Slack.Tests;

public class SlackStreamingResponseTests
{
    [Fact]
    public async Task ResetAsync_RestoresConfiguredDefaults()
    {
        var response = new SlackStreamingResponse(
            new Mock<ITurnContext>().Object,
            new SlackApi(new Mock<IHttpClientFactory>().Object));
        response.ConfigureDefaults(interval: 321, initialDelay: 123);

        response.Interval = 1;
        response.InitialDelay = 1;
        response.FeedbackLoopEnabled = true;

        await response.ResetAsync();

        Assert.True(response.IsStreamingChannel);
        Assert.Equal(321, response.Interval);
        Assert.Equal(123, response.InitialDelay);
        Assert.False(response.FeedbackLoopEnabled);
        Assert.Null(response.StreamId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EndStreamAsync_TimeoutFlushesUnsentText(bool prefixAlreadySent)
    {
        var requests = new List<(string Method, JsonElement Body)>();
        var response = CreateResponse(new TestHandler(async (request, _) =>
        {
            var method = request.RequestUri!.Segments[^1];
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            requests.Add((method, body.RootElement.Clone()));
            return CreateJsonResponse(method == "chat.startStream"
                ? """{"ok":true,"ts":"stream-ts"}"""
                : """{"ok":true}""");
        }));
        response.InitialDelay = prefixAlreadySent ? 1 : int.MaxValue;
        response.Interval = int.MaxValue;
        response.EndStreamTimeout = 0;

        if (prefixAlreadySent)
        {
            response.QueueTextChunk("prefix");
            await response.ChunkSent.Task;
            response.QueueTextChunk(" suffix");
        }
        else
        {
            response.QueueTextChunk("prefix suffix");
        }

        var result = await response.EndStreamAsync();

        Assert.Equal(StreamingResponseResult.Timeout, result);
        var stop = Assert.Single(requests, request => request.Method == "chat.stopStream");
        var chunks = stop.Body.GetProperty("chunks").EnumerateArray().ToArray();
        Assert.Contains(chunks, chunk =>
            chunk.GetProperty("type").GetString() == "markdown_text"
            && chunk.GetProperty("text").GetString() == (prefixAlreadySent ? " suffix" : "prefix suffix"));
        Assert.Contains(chunks, chunk =>
            chunk.GetProperty("type").GetString() == "task_update"
            && chunk.GetProperty("status").GetString() == "complete");
        Assert.False(response.IsStreamStarted());
        Assert.Null(response.StreamId);
    }

    [Theory]
    [InlineData("chat.startStream")]
    [InlineData("chat.appendStream")]
    public async Task EndStreamAsync_TimeoutCancelsInFlightRequestBeforeFinalizing(string blockedMethod)
    {
        var pendingResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestStarted = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestedMethods = new List<string>();
        var activeRequests = 0;
        var maxActiveRequests = 0;
        JsonElement finalBody = default;
        var handler = new TestHandler(async (request, cancellationToken) =>
        {
            var method = request.RequestUri!.Segments[^1];
            maxActiveRequests = Math.Max(maxActiveRequests, Interlocked.Increment(ref activeRequests));
            requestedMethods.Add(method);
            try
            {
                if (method == blockedMethod && !requestStarted.Task.IsCompleted)
                {
                    using var registration = cancellationToken.Register(() => pendingResponse.TrySetCanceled());
                    requestStarted.TrySetResult(cancellationToken);
                    return await pendingResponse.Task;
                }

                if (method == "chat.stopStream")
                {
                    using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
                    finalBody = body.RootElement.Clone();
                }

                return CreateJsonResponse(method == "chat.startStream"
                    ? """{"ok":true,"ts":"stream-ts"}"""
                    : """{"ok":true}""");
            }
            finally
            {
                Interlocked.Decrement(ref activeRequests);
            }
        });
        var response = CreateResponse(handler);
        response.InitialDelay = 1;
        response.EndStreamTimeout = 0;
        response.QueueTextChunk("content");
        var requestToken = await requestStarted.Task;

        var endTask = response.EndStreamAsync();
        try
        {
            Assert.True(requestToken.IsCancellationRequested);
            Assert.Equal(StreamingResponseResult.Timeout, await endTask);
            Assert.Equal(1, maxActiveRequests);
            Assert.Equal("chat.stopStream", requestedMethods.Last());
            Assert.Contains(finalBody.GetProperty("chunks").EnumerateArray(), chunk =>
                chunk.GetProperty("type").GetString() == "markdown_text"
                && chunk.GetProperty("text").GetString() == "content");
            Assert.Null(response.StreamId);
        }
        finally
        {
            pendingResponse.TrySetResult(CreateJsonResponse("""{"ok":true,"ts":"stream-ts"}"""));
            await endTask;
        }
    }

    private static TestSlackStreamingResponse CreateResponse(TestHandler handler)
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(factory => factory.CreateClient(nameof(SlackApi)))
            .Returns(() => new HttpClient(handler, disposeHandler: false));

        var activity = new Activity
        {
            Type = ActivityTypes.Message,
            ChannelId = Channels.Slack,
            ChannelData = JsonSerializer.Deserialize<object>(
                """{"SlackMessage":{"event":{"channel":"C1","ts":"T1"}},"ApiToken":"token"}""")
        };
        var turnContext = new Mock<ITurnContext>();
        turnContext.SetupGet(context => context.Activity).Returns(activity);

        return new TestSlackStreamingResponse(
            turnContext.Object,
            new SlackApi(httpClientFactory.Object));
    }

    private static HttpResponseMessage CreateJsonResponse(string json)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class TestSlackStreamingResponse(ITurnContext turnContext, SlackApi slackApi)
        : SlackStreamingResponse(turnContext, slackApi)
    {
        public TaskCompletionSource<bool> ChunkSent { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override void OnSendCompleted(bool isInformative, string text)
        {
            base.OnSendCompleted(isInformative, text);
            if (!isInformative)
            {
                ChunkSent.TrySetResult(true);
            }
        }
    }

    private sealed class TestHandler : DelegatingHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;

        public TestHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        {
            _send = send;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => _send(request, cancellationToken);
    }
}
