// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Abstract base for streaming response implementations.
    /// Owns the text buffer, interval timer, and stream state.
    /// Extension authors subclass this and implement the abstract channel-specific hooks.
    /// </summary>
    public abstract class StreamingResponseBase : IStreamingResponse
    {
        public static readonly int DefaultEndStreamTimeout = (int)TimeSpan.FromMinutes(2).TotalMilliseconds;

        private Timer? _timer;
        private bool _messageUpdated;
        private bool _streamStarted;
        private bool _streamEnded;
        private bool _streamCancelled;
        private bool _userCancelled;
        private readonly object _lock = new object();
        private readonly AutoResetEvent _queueEmpty = new AutoResetEvent(false);
        private int _sequenceNumber;

        /// <summary>Gets or sets the interval in ms between intermediate sends.</summary>
        public int Interval { get; set; } = 500;

        /// <summary>Gets or sets the timeout in ms for EndStreamAsync to wait for the buffer to drain.</summary>
        public int EndStreamTimeout { get; set; } = DefaultEndStreamTimeout;

        /// <summary>
        /// Whether the current channel supports intermediate streaming messages.
        /// Subclasses (or tests) set this directly. The base may set it to false on FallbackToNonStreaming.
        /// </summary>
        public bool IsStreamingChannel { get; protected set; }

        /// <summary>
        /// Gets the stream ID. Assignment is the subclass's responsibility via the protected set.
        /// The base only resets to string.Empty in ResetAsync.
        /// Teams sets this from response.Id of the first SendActivity call.
        /// Other channels may set it to Guid.NewGuid().ToString() in their constructor or on first send.
        /// </summary>
        public string StreamId { get; protected set; } = string.Empty;

        /// <summary>Gets the accumulated message text (raw, unformatted).</summary>
        public string Message { get; protected set; } = string.Empty;

        // ── General optional features ─────────────────────────────────────────

        /// <inheritdoc/>
        public virtual bool FeedbackLoopEnabled { get; set; }

        /// <inheritdoc/>
        public virtual string FeedbackLoopType { get; set; } = "default";

        // ── Citations — managed in base; channel-specific rendering in hooks ──

        /// <inheritdoc/>
        public virtual List<ClientCitation>? Citations { get; protected set; } = new List<ClientCitation>();

        /// <inheritdoc/>
        public virtual void AddCitation(ClientCitation citation)
        {
            Citations ??= new List<ClientCitation>();
            Citations.Add(citation);
        }

        /// <inheritdoc/>
        public virtual void AddCitation(Citation citation, int citationPosition)
        {
            Citations ??= new List<ClientCitation>();
            Citations.Add(new ClientCitation()
            {
                Position = citationPosition,
                Appearance = new ClientCitationAppearance()
                {
                    Name = citation.Title ?? $"Document #{citationPosition}",
                    Abstract = CitationUtils.Snippet(citation.Content, 480),
                    Url = citation.Url
                }
            });
        }

        /// <inheritdoc/>
        public virtual void AddCitations(IList<Citation> citations)
        {
            if (citations.Count > 0)
            {
                Citations ??= new List<ClientCitation>();
                int currPos = Citations.Count;
                foreach (Citation citation in citations)
                {
                    Citations.Add(new ClientCitation()
                    {
                        Position = currPos + 1,
                        Appearance = new ClientCitationAppearance()
                        {
                            Name = citation.Title ?? $"Document #{currPos + 1}",
                            Abstract = CitationUtils.Snippet(citation.Content, 480),
                            Url = citation.Url
                        }
                    });
                    currPos++;
                }
            }
        }

        /// <inheritdoc/>
        public virtual void AddCitations(IList<ClientCitation> citations)
        {
            if (citations.Count > 0)
            {
                Citations ??= new List<ClientCitation>();
                Citations.AddRange(citations);
            }
        }

        // ── Teams-specific — virtual no-op defaults ───────────────────────────

        /// <inheritdoc/>
        public virtual IActivity FinalMessage { get; set; }

        /// <inheritdoc/>
        public virtual bool? EnableGeneratedByAILabel { get; set; }

        /// <inheritdoc/>
        public virtual SensitivityUsageInfo? SensitivityLabel { get; set; }

        // ── Shared behavior ───────────────────────────────────────────────────

        /// <inheritdoc/>
        public virtual void QueueTextChunk(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            lock (_lock)
            {
                if (_streamEnded)
                    throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(
                        ErrorHelper.StreamingResponseEnded, null);

                Message += text;
                _messageUpdated = true;
                StartStream(250);
            }
        }

        /// <inheritdoc/>
        public async Task QueueInformativeUpdateAsync(string text, CancellationToken cancellationToken = default)
        {
            if (!IsStreamingChannel)
                return;

            lock (_lock)
            {
                if (_streamEnded)
                    throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(
                        ErrorHelper.StreamingResponseEnded, null);
            }

            await SendInformativeAsync(text, cancellationToken).ConfigureAwait(false);

            lock (_lock)
            {
                // Mark stream as started so IsStreamStarted() returns true and EndStreamAsync waits
                StartStream();
            }
        }

        /// <inheritdoc/>
        public async Task<StreamingResponseResult> EndStreamAsync(CancellationToken cancellationToken = default)
        {
            if (!IsStreamingChannel)
            {
                lock (_lock)
                {
                    if (_streamEnded)
                        return StreamingResponseResult.AlreadyEnded;
                    _streamEnded = true;
                }

                await FinalizeStreamAsync(cancellationToken).ConfigureAwait(false);
                return StreamingResponseResult.Success;
            }
            else
            {
                lock (_lock)
                {
                    if (_streamEnded)
                        return StreamingResponseResult.AlreadyEnded;
                    _streamEnded = true;

                    if (_streamCancelled)
                        return _userCancelled ? StreamingResponseResult.UserCancelled : StreamingResponseResult.Error;

                    if (!_streamStarted)
                        return StreamingResponseResult.NotStarted;
                }

                // Wait for timer to drain the buffer
                if (!_queueEmpty.WaitOne(EndStreamTimeout))
                    return StreamingResponseResult.Timeout;

                if (_streamCancelled)
                    return _userCancelled ? StreamingResponseResult.UserCancelled : StreamingResponseResult.Error;

                await FinalizeStreamAsync(cancellationToken).ConfigureAwait(false);
                return StreamingResponseResult.Success;
            }
        }

        /// <inheritdoc/>
        public bool IsStreamStarted() => _streamStarted;

        /// <inheritdoc/>
        public int UpdatesSent() => _sequenceNumber;

        /// <summary>
        /// Resets shared base state for reuse. Subclasses must call base.ResetAsync() then reset their own state.
        /// Does NOT reset IsStreamingChannel — that is the subclass's responsibility.
        /// </summary>
        public virtual async Task ResetAsync(CancellationToken cancellationToken = default)
        {
            if (IsStreamStarted())
                await EndStreamAsync(cancellationToken).ConfigureAwait(false);

            lock (_lock)
            {
                StopStream();
                _streamEnded = false;
                _streamStarted = false;
                _streamCancelled = false;
                _userCancelled = false;
                _messageUpdated = false;
                _sequenceNumber = 0;
                Message = string.Empty;
                StreamId = string.Empty;
                Citations = new List<ClientCitation>();
            }
        }

        // ── Abstract hooks ────────────────────────────────────────────────────

        /// <summary>
        /// Called by the timer thread with accumulated text. Send as a channel-specific intermediate message.
        /// </summary>
        protected abstract Task SendChunksAsync(string bufferedText, int sequenceNumber, CancellationToken cancellationToken);

        /// <summary>
        /// Called on the caller's thread when QueueInformativeUpdateAsync is called on a streaming channel.
        /// Send a "thinking/working" update to the channel. Implement as no-op if unsupported.
        /// </summary>
        protected abstract Task SendInformativeAsync(string text, CancellationToken cancellationToken);

        /// <summary>
        /// Called after the buffer drains (streaming) or immediately (non-streaming) by EndStreamAsync.
        /// Send the final message, call channel stop API, or no-op.
        /// For FallbackToNonStreaming: IsStreamingChannel will be false — check it to decide format.
        /// </summary>
        protected abstract Task FinalizeStreamAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Called on the timer thread when SendChunksAsync throws. Interpret the error and return
        /// a StreamErrorAction. The base class applies the action.
        /// </summary>
        protected abstract Task<StreamErrorAction> HandleSendErrorAsync(Exception ex, CancellationToken cancellationToken);

        // ── Timer management ─────────────────────────────────────────────────

        private void StartStream(int initialInterval = 0)
        {
            if (_timer == null && IsStreamingChannel)
            {
                _streamStarted = true;
                _timer = new Timer(OnTimerTick, null,
                    initialInterval == 0 ? Interval : initialInterval,
                    Timeout.Infinite);
            }
        }

        private void StopStream()
        {
            _timer?.Dispose();
            _timer = null;
        }

        private async void OnTimerTick(object? state)
        {
            string? textToSend = null;
            int seqToSend = 0;

            lock (_lock)
            {
                if (_messageUpdated)
                {
                    textToSend = Message;
                    seqToSend = ++_sequenceNumber;
                    _messageUpdated = false;
                    _timer?.Change(Timeout.Infinite, Timeout.Infinite); // pause during send
                }
                else if (_streamEnded)
                {
                    _queueEmpty.Set();
                    StopStream();
                    return;
                }
                else
                {
                    // No new text yet — poll faster while waiting for chunks
                    _timer?.Change(200, Timeout.Infinite);
                    return;
                }
            }

            try
            {
                await SendChunksAsync(textToSend!, seqToSend, CancellationToken.None).ConfigureAwait(false);

                lock (_lock)
                {
                    if (_streamEnded && !_messageUpdated)
                    {
                        _queueEmpty.Set();
                        StopStream();
                    }
                    else
                    {
                        _timer?.Change(Interval, Timeout.Infinite); // restart
                    }
                }
            }
            catch (Exception ex)
            {
                // Cannot rethrow — we're on the timer thread. Call the error hook.
                var action = await HandleSendErrorAsync(ex, CancellationToken.None).ConfigureAwait(false);

                lock (_lock)
                {
                    switch (action)
                    {
                        case StreamErrorAction.FallbackToNonStreaming:
                            IsStreamingChannel = false;
                            break;
                        case StreamErrorAction.Cancel:
                            _streamCancelled = true;
                            _userCancelled = true;
                            break;
                        // Continue: do nothing, keep streaming (but timer was already paused — restart it)
                        default:
                            _timer?.Change(Interval, Timeout.Infinite);
                            return;
                    }
                    StopStream();
                    _queueEmpty.Set();
                }
            }
        }
    }
}
