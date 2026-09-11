// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Slack.Api;
using Moq;
using System;
using System.Collections.Generic;
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

    [Fact]
    public async Task EndStreamAsync_TimeoutWaitsForInFlightStartBeforeStoppingStream()
    {
        var startResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var startRequested = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestedMethods = new List<string>();
        var handler = new TestHandler((request, _) =>
        {
            var method = request.RequestUri!.Segments[^1];
            lock (requestedMethods)
            {
                requestedMethods.Add(method);
            }

            if (method == "chat.startStream")
            {
                startRequested.TrySetResult(true);
                return startResponse.Task;
            }

            return Task.FromResult(CreateJsonResponse("""{"ok":true}"""));
        });
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

        var response = new SlackStreamingResponse(
            turnContext.Object,
            new SlackApi(httpClientFactory.Object))
        {
            InitialDelay = 1,
            EndStreamTimeout = 0
        };
        response.QueueTextChunk("content");
        await startRequested.Task;

        var endTask = response.EndStreamAsync();

        Assert.False(endTask.IsCompleted);

        startResponse.SetResult(CreateJsonResponse("""{"ok":true,"ts":"stream-ts"}"""));
        var result = await endTask;

        Assert.Equal(StreamingResponseResult.Timeout, result);
        lock (requestedMethods)
        {
            Assert.Equal(
                ["chat.startStream", "chat.appendStream", "chat.appendStream", "chat.stopStream"],
                requestedMethods);
        }
    }

    private static HttpResponseMessage CreateJsonResponse(string json)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

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
