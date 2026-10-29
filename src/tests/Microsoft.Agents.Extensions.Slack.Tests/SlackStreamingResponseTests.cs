// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Slack.Api;
using Microsoft.Extensions.Time.Testing;
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
    public async Task ChannelStream_IncludesRecipientAndWorkspace()
    {
        var handler = new RecordingHandler();
        var response = CreateResponse(handler);

        await response.QueueInformativeUpdateAsync("Working");
        response.QueueTextChunk("answer");
        Assert.Equal(StreamingResponseResult.Success, await response.EndStreamAsync());

        var start = Assert.Single(handler.Requests, request => request.Method == "chat.startStream");
        Assert.Equal("U1", start.Body.GetProperty("recipient_user_id").GetString());
        Assert.Equal("T1", start.Body.GetProperty("recipient_team_id").GetString());
        Assert.Equal("thread-ts", start.Body.GetProperty("thread_ts").GetString());
    }

    [Fact]
    public async Task AttachmentOnlyResponse_MergesImageAndFeedbackAtCompletion()
    {
        var handler = new RecordingHandler();
        var response = CreateResponse(handler);
        response.FeedbackLoopEnabled = true;
        response.AddAttachment(new Attachment
        {
            ContentType = "image/png",
            ContentUrl = "https://example.com/image.png",
            Name = "An image"
        });

        Assert.Equal(StreamingResponseResult.Success, await response.EndStreamAsync());

        var blocks = FinalBlocks(handler);
        Assert.Contains(blocks, block => block.GetProperty("type").GetString() == "image"
            && block.GetProperty("image_url").GetString() == "https://example.com/image.png");
        Assert.Contains(blocks, block => block.GetProperty("type").GetString() == "context_actions");
        Assert.False(response.IsStreamStarted());
        Assert.Null(response.StreamId);
    }

    [Fact]
    public async Task HeroCard_ProducesTextImageAndOpenUrlButton()
    {
        var handler = new RecordingHandler();
        var response = CreateResponse(handler);
        response.AddAttachment(new HeroCard
        {
            Title = "A card",
            Text = "Details",
            Images = [new CardImage("https://example.com/card.png")],
            Buttons = [new CardAction(ActionTypes.OpenUrl, "Open", value: "https://example.com/details")]
        }.ToAttachment());

        Assert.Equal(StreamingResponseResult.Success, await response.EndStreamAsync());

        var blocks = FinalBlocks(handler);
        Assert.Contains(blocks, block => block.GetRawText().Contains("A card"));
        Assert.Contains(blocks, block => block.GetProperty("type").GetString() == "image");
        Assert.Contains(blocks, block => block.GetProperty("type").GetString() == "actions"
            && block.GetProperty("elements")[0].GetProperty("url").GetString() == "https://example.com/details");
    }

    [Theory]
    [InlineData(ContentTypes.AdaptiveCard)]
    [InlineData(ContentTypes.OAuthCard)]
    public void UnsupportedAttachment_IsRejectedBeforeStartingStream(string contentType)
    {
        var handler = new RecordingHandler();
        var response = CreateResponse(handler);

        Assert.Throws<NotSupportedException>(() => response.AddAttachment(
            new Attachment { ContentType = contentType, Content = new { type = "unsupported" } }));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SplitCitationMarkers_UseLateMetadataWithoutRewritingSentText()
    {
        var handler = new RecordingHandler();
        var response = CreateResponse(handler);
        await response.QueueInformativeUpdateAsync("Working");
        response.QueueTextChunk("Answer [do");
        await handler.FirstText.Task.WaitAsync(TimeSpan.FromSeconds(10));
        response.QueueTextChunk("c1] and [1].");
        response.AddCitation(new ClientCitation
        {
            Position = 1,
            Appearance = new ClientCitationAppearance { Name = "Policy", Url = "https://example.com/policy" }
        });
        response.AddCitation(new ClientCitation
        {
            Position = 2,
            Appearance = new ClientCitationAppearance { Name = "Unused", Url = "https://example.com/unused" }
        });

        Assert.Equal(StreamingResponseResult.Success, await response.EndStreamAsync());

        Assert.Equal("Answer [1] and [1].", StreamedText(handler));
        var sources = FinalBlocks(handler).Where(block => block.GetProperty("type").GetString() == "markdown")
            .Select(block => block.GetProperty("text").GetString()).ToArray();
        Assert.Single(sources, text => text.Contains("https://example.com/policy"));
        Assert.DoesNotContain(sources, text => text.Contains("Unused"));
    }

    [Fact]
    public async Task CitationOnlyResponse_DeliversSourcesWithoutDummyText()
    {
        var handler = new RecordingHandler();
        var response = CreateResponse(handler);
        response.AddCitation(new Citation("An extract", "Only source", "https://example.com/source"), 1);

        Assert.Equal(StreamingResponseResult.Success, await response.EndStreamAsync());
        Assert.Contains(FinalBlocks(handler), block => block.GetRawText().Contains("https://example.com/source"));
        Assert.Equal("", StreamedText(handler));
    }

    [Fact]
    public async Task SourcesAtTextLimit_MoveToAnotherStream()
    {
        var handler = new RecordingHandler();
        var response = CreateResponse(handler);
        response.AddCitation(new Citation("An extract", "Policy", "https://example.com/policy"), 1);
        response.QueueTextChunk(new string('a', 11997) + "[1]");

        Assert.Equal(StreamingResponseResult.Success, await response.EndStreamAsync());
        Assert.Equal(2, handler.Requests.Count(request => request.Method == "chat.startStream"));
        Assert.Contains(FinalBlocks(handler), block => block.GetRawText().Contains("https://example.com/policy"));
    }

    [Fact]
    public async Task IdleStream_ClosesBeforeExpiryAndResumesInSameThread()
    {
        var clock = new FakeTimeProvider();
        var handler = new RecordingHandler();
        var response = CreateResponse(handler, clock);
        await response.QueueInformativeUpdateAsync("Working");

        clock.Advance(TimeSpan.FromMinutes(10));
        await handler.FirstStop.Task.WaitAsync(TimeSpan.FromSeconds(10));
        clock.AutoAdvanceAmount = TimeSpan.FromSeconds(2);
        response.QueueTextChunk("after cutoff");

        Assert.Equal(StreamingResponseResult.Success, await response.EndStreamAsync());
        Assert.Equal(2, handler.Requests.Count(request => request.Method == "chat.startStream"));
        Assert.Equal("processing", handler.Requests.First(request =>
            request.Method == "chat.stopStream").Body.GetProperty("session_status").GetString());
        Assert.Equal("after cutoff", StreamedText(handler));
    }

    [Fact]
    public async Task LongResponse_RollsOverWithoutLosingOrDuplicatingText()
    {
        var handler = new RecordingHandler();
        var response = CreateResponse(handler);
        var text = new string('a', 8000) + new string('b', 5000);
        response.QueueTextChunk(text);

        Assert.Equal(StreamingResponseResult.Success, await response.EndStreamAsync());

        Assert.Equal(text, StreamedText(handler));
        Assert.Equal(2, handler.Requests.Count(request => request.Method == "chat.startStream"));
        var stops = handler.Requests.Where(request => request.Method == "chat.stopStream").ToArray();
        Assert.Equal("processing", stops[0].Body.GetProperty("session_status").GetString());
        Assert.All(handler.Requests.Where(request => request.Body.TryGetProperty("ts", out _))
            .GroupBy(request => request.Body.GetProperty("ts").GetString()),
            stream => Assert.InRange(stream.SelectMany(TextChunks).Sum(chunk => chunk.Length), 0, 12000));
    }

    [Fact]
    public async Task ClientStop_ReportsUserCancelledWithoutOpeningAnotherStream()
    {
        var handler = new RecordingHandler
        {
            Reply = (method, _) => method == "chat.appendStream"
                ? JsonResponse("""{"ok":false,"error":"stopped_by_user"}""")
                : null
        };
        var response = CreateResponse(handler);

        await response.QueueInformativeUpdateAsync("Working");
        response.QueueTextChunk("must not be resent");

        Assert.Equal(StreamingResponseResult.UserCancelled, await response.EndStreamAsync());
        Assert.Single(handler.Requests, request => request.Method == "chat.startStream");
    }

    [Fact]
    public async Task ClientStopDuringFinalization_ReportsUserCancelled()
    {
        var handler = new RecordingHandler
        {
            Reply = (method, _) => method == "chat.stopStream"
                ? JsonResponse("""{"ok":false,"error":"stopped_by_user"}""")
                : null
        };
        var response = CreateResponse(handler);
        response.QueueTextChunk("answer");

        Assert.Equal(StreamingResponseResult.UserCancelled, await response.EndStreamAsync());
        Assert.Null(response.StreamId);
        Assert.Single(handler.Requests, request => request.Method == "chat.startStream");
    }

    [Fact]
    public async Task FailedStop_CanBeRetriedWithoutSendingTextTwice()
    {
        var stopAttempts = 0;
        var handler = new RecordingHandler
        {
            Reply = (method, _) => method == "chat.stopStream" && ++stopAttempts <= 3
                ? Throttled()
                : null
        };
        var response = CreateResponse(handler);
        response.QueueTextChunk("answer");

        await Assert.ThrowsAsync<SlackResponseException>(() => response.EndStreamAsync());
        Assert.NotNull(response.StreamId);
        Assert.Equal(StreamingResponseResult.Success, await response.EndStreamAsync());

        Assert.Equal("answer", StreamedText(handler));
        Assert.Equal(4, stopAttempts);
        Assert.Null(response.StreamId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExpiryDuringFinalStop_PreservesUncommittedBlocksWithoutReplayingText(bool hasAttachment)
    {
        var clock = new FakeTimeProvider { AutoAdvanceAmount = TimeSpan.FromSeconds(2) };
        var stops = 0;
        var handler = new RecordingHandler
        {
            Reply = (method, _) =>
            {
                if (method == "chat.stopStream" && ++stops == 1)
                {
                    clock.Advance(TimeSpan.FromMinutes(10));
                    return JsonResponse("""{"ok":false,"error":"message_not_in_streaming_state"}""");
                }
                return null;
            }
        };
        var response = CreateResponse(handler, clock);
        response.QueueTextChunk("answer");
        if (hasAttachment)
        {
            response.AddAttachment(new Attachment
            {
                ContentType = "image/png",
                ContentUrl = "https://example.com/image.png"
            });
        }

        Assert.Equal(StreamingResponseResult.Success, await response.EndStreamAsync());
        Assert.Equal("answer", StreamedText(handler));
        Assert.Equal(hasAttachment ? 2 : 1,
            handler.Requests.Count(request => request.Method == "chat.startStream"));
        Assert.Null(response.StreamId);
        if (hasAttachment)
        {
            var final = handler.Requests.Last(request => request.Method == "chat.stopStream");
            Assert.Equal("image", Assert.Single(final.Body.GetProperty("blocks").EnumerateArray())
                .GetProperty("type").GetString());
        }
    }

    [Fact]
    public async Task UnexpectedClosedStream_IsTerminalInsteadOfRetryingTheSameTimestampForever()
    {
        var handler = new RecordingHandler
        {
            Reply = (method, _) => method == "chat.stopStream"
                ? JsonResponse("""{"ok":false,"error":"message_not_in_streaming_state"}""")
                : null
        };
        var response = CreateResponse(handler);
        response.QueueTextChunk("answer");

        Assert.Equal(StreamingResponseResult.Error, await response.EndStreamAsync());
        Assert.Equal(StreamingResponseResult.AlreadyEnded, await response.EndStreamAsync());
        Assert.Null(response.StreamId);
        Assert.Single(handler.Requests, request => request.Method == "chat.stopStream");
    }

    [Fact]
    public async Task RepeatedFinalExpiry_StopsRecoveryAndReportsError()
    {
        var clock = new FakeTimeProvider { AutoAdvanceAmount = TimeSpan.FromSeconds(2) };
        var handler = new RecordingHandler
        {
            Reply = (method, _) =>
            {
                if (method == "chat.stopStream")
                {
                    clock.Advance(TimeSpan.FromMinutes(10));
                    return JsonResponse("""{"ok":false,"error":"message_not_in_streaming_state"}""");
                }
                return null;
            }
        };
        var response = CreateResponse(handler, clock);
        response.AddAttachment(new Attachment { ContentType = "image/png", ContentUrl = "https://example.com/image.png" });

        Assert.Equal(StreamingResponseResult.Error, await response.EndStreamAsync());
        Assert.Equal(3, handler.Requests.Count(request => request.Method == "chat.stopStream"));
        Assert.Equal(StreamingResponseResult.AlreadyEnded, await response.EndStreamAsync());
        Assert.Null(response.StreamId);
    }

    [Fact]
    public async Task InvalidFinalMetadata_CannotSilentlyBecomeSuccessOnRetry()
    {
        var handler = new RecordingHandler();
        var response = CreateResponse(handler);
        response.AddCitation(new ClientCitation
        {
            Position = 1,
            Appearance = new ClientCitationAppearance { Name = new string('x', 13000) }
        });
        response.QueueTextChunk("answer [1]");
        try
        {
            await Assert.ThrowsAsync<NotSupportedException>(() => response.EndStreamAsync());
            await Assert.ThrowsAsync<NotSupportedException>(() => response.EndStreamAsync());
        }
        finally
        {
            response.Citations.Clear();
            await response.EndStreamAsync();
        }
    }

    [Fact]
    public async Task Reset_ClearsPayloadAndUsesANewRemoteStream()
    {
        var handler = new RecordingHandler();
        var response = CreateResponse(handler);
        response.FeedbackLoopEnabled = true;
        response.QueueTextChunk("first");
        await response.EndStreamAsync();
        var firstStop = handler.Requests.FindIndex(request => request.Method == "chat.stopStream");

        await response.ResetAsync();
        response.QueueTextChunk("second");
        Assert.Equal(StreamingResponseResult.Success, await response.EndStreamAsync());

        var second = handler.Requests.Skip(firstStop + 1).ToArray();
        Assert.Equal("second", string.Concat(second.SelectMany(TextChunks)));
        Assert.DoesNotContain(second, request => request.Body.GetRawText().Contains("feedback_buttons"));
        Assert.Equal(2, handler.Requests.Count(request => request.Method == "chat.startStream"));
    }

    [Fact]
    public async Task CancelledQueuedInformative_DoesNotWaitForAnUnrelatedSend()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RecordingHandler
        {
            HandleAsync = async (method, body, cancellationToken) =>
            {
                if (method == "chat.appendStream" && TextChunks((method, body)).Any())
                {
                    using var registration = cancellationToken.Register(() => release.TrySetCanceled(cancellationToken));
                    started.TrySetResult(true);
                    return await release.Task;
                }
                return null;
            }
        };
        var response = CreateResponse(handler);
        response.QueueTextChunk("answer");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using var cancellation = new CancellationTokenSource();
        var informative = response.QueueInformativeUpdateAsync("cancel this status", cancellation.Token);
        cancellation.Cancel();
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => informative.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            release.TrySetResult(JsonResponse("""{"ok":true}"""));
            try { await informative; } catch (OperationCanceledException) { }
            await response.EndStreamAsync();
        }

        Assert.Equal("answer", StreamedText(handler));
        Assert.DoesNotContain(handler.Requests, request => request.Body.GetRawText().Contains("cancel this status"));
    }

    [Theory]
    [InlineData("chat.startStream")]
    [InlineData("chat.appendStream")]
    public async Task DrainTimeout_DoesNotReplayAnAmbiguousRequest(string blockedMethod)
    {
        var requestStarted = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var maximumActive = 0;
        var handler = new RecordingHandler
        {
            HandleAsync = async (method, _, cancellationToken) =>
            {
                maximumActive = Math.Max(maximumActive, Interlocked.Increment(ref active));
                try
                {
                    if (method == blockedMethod && !requestStarted.Task.IsCompleted)
                    {
                        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
                        using var registration = cancellationToken.Register(() =>
                        {
                            cancelled.TrySetResult(true);
                            pending.TrySetCanceled(cancellationToken);
                        });
                        requestStarted.TrySetResult(cancellationToken);
                        return await pending.Task;
                    }

                    return null;
                }
                finally
                {
                    Interlocked.Decrement(ref active);
                }
            }
        };
        var response = CreateResponse(handler);
        response.EndStreamTimeout = 0;
        response.QueueTextChunk("content");
        var requestToken = await requestStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var ending = response.EndStreamAsync();
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(requestToken.IsCancellationRequested);
        Assert.Equal(StreamingResponseResult.Timeout, await ending);
        Assert.Equal(1, maximumActive);
        Assert.Single(handler.Requests, request => request.Method == blockedMethod);
        if (blockedMethod == "chat.appendStream")
        {
            Assert.Equal("chat.stopStream", handler.Requests.Last().Method);
        }
        else
        {
            Assert.DoesNotContain(handler.Requests, request => request.Method == "chat.stopStream");
        }
        Assert.Null(response.StreamId);
    }

    private static SlackStreamingResponse CreateResponse(RecordingHandler handler, FakeTimeProvider clock = null)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient(nameof(SlackApi)))
            .Returns(() => new HttpClient(handler, disposeHandler: false));
        var activity = new Activity
        {
            Type = ActivityTypes.Message,
            ChannelId = Channels.Slack,
            ChannelData = JsonSerializer.Deserialize<object>(
                """{"SlackMessage":{"team_id":"T1","api_app_id":"A1","event":{"channel":"C1","user":"U1","ts":"thread-ts"}},"ApiToken":"token"}""")
        };
        var context = new Mock<ITurnContext>();
        context.SetupGet(value => value.Activity).Returns(activity);
        clock ??= new FakeTimeProvider { AutoAdvanceAmount = TimeSpan.FromSeconds(2) };
        return new SlackStreamingResponse(context.Object, new SlackApi(factory.Object, null, new SlackRateLimiter(clock)))
        {
            Interval = 1,
            InitialDelay = 1,
            EndStreamTimeout = int.MaxValue
        };
    }

    private static JsonElement[] FinalBlocks(RecordingHandler handler)
        => handler.Requests.Where(request => request.Method == "chat.stopStream"
            && request.Body.TryGetProperty("blocks", out _))
            .SelectMany(request => request.Body.GetProperty("blocks").EnumerateArray()).ToArray();

    private static IEnumerable<string> TextChunks((string Method, JsonElement Body) request)
        => request.Body.TryGetProperty("chunks", out var chunks)
            ? chunks.EnumerateArray().Where(chunk => chunk.GetProperty("type").GetString() == "markdown_text")
                .Select(chunk => chunk.GetProperty("text").GetString())
            : [];

    private static string StreamedText(RecordingHandler handler)
        => string.Concat(handler.Requests.Where(request =>
            request.Method is "chat.appendStream" or "chat.stopStream").SelectMany(TextChunks));

    private static HttpResponseMessage JsonResponse(string json)
        => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Throttled()
    {
        var response = JsonResponse("""{"ok":false,"error":"ratelimited"}""");
        response.StatusCode = HttpStatusCode.TooManyRequests;
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
        return response;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private int _streams;
        public List<(string Method, JsonElement Body)> Requests { get; } = [];
        public TaskCompletionSource<bool> FirstText { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> FirstStop { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Func<string, JsonElement, HttpResponseMessage> Reply { get; init; }
        public Func<string, JsonElement, CancellationToken, Task<HttpResponseMessage>> HandleAsync { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var method = request.RequestUri.Segments[^1];
            using var document = JsonDocument.Parse(await request.Content.ReadAsStringAsync(cancellationToken));
            var body = document.RootElement.Clone();
            Requests.Add((method, body));
            var response = HandleAsync != null ? await HandleAsync(method, body, cancellationToken) : Reply?.Invoke(method, body);
            response ??= JsonResponse(method == "chat.startStream"
                ? $$"""{"ok":true,"ts":"stream-{{Interlocked.Increment(ref _streams)}}"}"""
                : """{"ok":true}""");
            if (TextChunks((method, body)).Any())
            {
                FirstText.TrySetResult(true);
            }
            if (method == "chat.stopStream")
            {
                FirstStop.TrySetResult(true);
            }
            return response;
        }
    }
}
