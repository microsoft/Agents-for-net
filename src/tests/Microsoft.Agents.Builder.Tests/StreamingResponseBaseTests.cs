// src/tests/Microsoft.Agents.Builder.Tests/StreamingResponseBaseTests.cs
using Microsoft.Agents.Builder;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests
{
    public class StreamingResponseBaseTests
    {
        // ── Minimal test subclass ──────────────────────────────────────────────
        private class TestStreamingResponse : StreamingResponseBase
        {
            public List<(string Text, int Seq)> SentChunks = new();
            public List<string> SentInformatives = new();
            public bool Finalized;
            public StreamErrorAction ErrorAction = StreamErrorAction.Continue;
            public Exception ThrownOnNextSend;

            public TestStreamingResponse(bool isStreamingChannel = false, int interval = 500)
            {
                IsStreamingChannel = isStreamingChannel;
                Interval = interval;
            }

            protected override Task SendChunksAsync(string bufferedText, int sequenceNumber, CancellationToken ct)
            {
                if (ThrownOnNextSend != null)
                {
                    var ex = ThrownOnNextSend;
                    ThrownOnNextSend = null;
                    throw ex;
                }
                SentChunks.Add((bufferedText, sequenceNumber));
                return Task.CompletedTask;
            }

            protected override Task SendInformativeAsync(string text, int sequenceNumber, CancellationToken ct)
            {
                SentInformatives.Add(text);
                return Task.CompletedTask;
            }

            protected override Task FinalizeStreamAsync(CancellationToken ct)
            {
                Finalized = true;
                return Task.CompletedTask;
            }

            protected override Task<StreamErrorAction> HandleSendErrorAsync(Exception ex, CancellationToken ct)
                => Task.FromResult(ErrorAction);
        }
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void QueueTextChunk_AccumulatesInMessage()
        {
            var sr = new TestStreamingResponse(true, 5000);
            sr.QueueTextChunk("Hello");
            sr.QueueTextChunk(" World");
            Assert.Equal("Hello World", sr.Message);
        }

        [Fact]
        public async Task QueueTextChunk_AfterStreamEnded_Throws()
        {
            var sr = new TestStreamingResponse(false, 100);
            sr.QueueTextChunk("text");
            await sr.EndStreamAsync();
            Assert.Throws<InvalidOperationException>(() => sr.QueueTextChunk("more"));
        }

        [Fact]
        public void QueueTextChunk_Empty_IsIgnored()
        {
            var sr = new TestStreamingResponse(false);
            sr.QueueTextChunk("");
            sr.QueueTextChunk(null);
            Assert.Equal("", sr.Message);
        }

        [Fact]
        public async Task QueueInformativeUpdateAsync_OnStreamingChannel_CallsSendInformativeAsync()
        {
            var sr = new TestStreamingResponse(true, 5000);
            await sr.QueueInformativeUpdateAsync("Thinking...");
            Assert.Single(sr.SentInformatives);
            Assert.Equal("Thinking...", sr.SentInformatives[0]);
        }

        [Fact]
        public async Task QueueInformativeUpdateAsync_OnNonStreamingChannel_IsNoOp()
        {
            var sr = new TestStreamingResponse(false);
            await sr.QueueInformativeUpdateAsync("Thinking...");
            Assert.Empty(sr.SentInformatives);
        }

        [Fact]
        public async Task EndStreamAsync_NonStreaming_CallsFinalize_AndReturnsSuccess()
        {
            var sr = new TestStreamingResponse(false);
            sr.QueueTextChunk("hello");
            var result = await sr.EndStreamAsync();
            Assert.True(sr.Finalized);
            Assert.Equal(StreamingResponseResult.Success, result);
        }

        [Fact]
        public async Task EndStreamAsync_WhenNothingQueued_ReturnsNotStarted()
        {
            var sr = new TestStreamingResponse(true, 100);
            var result = await sr.EndStreamAsync();
            Assert.Equal(StreamingResponseResult.NotStarted, result);
        }

        [Fact]
        public async Task EndStreamAsync_CalledTwice_ReturnsAlreadyEnded()
        {
            var sr = new TestStreamingResponse(false);
            sr.QueueTextChunk("text");
            await sr.EndStreamAsync();
            var result = await sr.EndStreamAsync();
            Assert.Equal(StreamingResponseResult.AlreadyEnded, result);
        }

        [Fact]
        public async Task EndStreamAsync_StreamingChannel_SendsChunksAndFinalizes()
        {
            var sr = new TestStreamingResponse(true, 100);
            sr.QueueTextChunk("hello");
            await Task.Delay(200); // let timer fire
            sr.QueueTextChunk(" world");
            var result = await sr.EndStreamAsync();
            Assert.Equal(StreamingResponseResult.Success, result);
            Assert.True(sr.SentChunks.Count >= 1);
            Assert.True(sr.Finalized);
        }

        [Fact]
        public async Task ResetAsync_ClearsStateForReuse()
        {
            var sr = new TestStreamingResponse(false);
            sr.QueueTextChunk("text");
            await sr.EndStreamAsync();
            await sr.ResetAsync();

            Assert.Equal("", sr.Message);
            Assert.False(sr.IsStreamStarted());
            // Stream can be used again after reset
            sr.QueueTextChunk("new");
            Assert.Equal("new", sr.Message);
        }

        [Fact]
        public async Task HandleSendErrorAsync_FallbackToNonStreaming_DisablesStreaming()
        {
            var sr = new TestStreamingResponse(true, 100)
            {
                ErrorAction = StreamErrorAction.FallbackToNonStreaming,
                ThrownOnNextSend = new Exception("streaming not supported")
            };
            sr.QueueTextChunk("text");
            var result = await sr.EndStreamAsync();
            Assert.False(sr.IsStreamingChannel);
            // Finalize is still called after fallback
            Assert.True(sr.Finalized);
        }

        [Fact]
        public async Task HandleSendErrorAsync_Cancel_ReturnsUserCancelled()
        {
            var sr = new TestStreamingResponse(true, 100)
            {
                ErrorAction = StreamErrorAction.Cancel,
                ThrownOnNextSend = new Exception("user cancelled")
            };
            sr.QueueTextChunk("text");
            var result = await sr.EndStreamAsync();
            Assert.Equal(StreamingResponseResult.UserCancelled, result);
        }

        [Fact]
        public void IsStreamStarted_BeforeAnyChunk_ReturnsFalse()
        {
            var sr = new TestStreamingResponse(true, 5000);
            Assert.False(sr.IsStreamStarted());
        }

        [Fact]
        public void UpdatesSent_ReflectsChunksSent()
        {
            var sr = new TestStreamingResponse(true, 5000);
            Assert.Equal(0, sr.UpdatesSent());
        }

        [Fact]
        public void AddCitation_AddsToList()
        {
            var sr = new TestStreamingResponse(false);
            sr.AddCitation(new Microsoft.Agents.Core.Models.ClientCitation());
            Assert.Single(sr.Citations!);
        }

        [Fact]
        public void OptionalProperties_AreSettableOnBase()
        {
            var sr = new TestStreamingResponse(false);
            // These properties have backing storage in the base — should not throw
            sr.FeedbackLoopEnabled = true;
            sr.EnableGeneratedByAILabel = true;
        }
    }
}
