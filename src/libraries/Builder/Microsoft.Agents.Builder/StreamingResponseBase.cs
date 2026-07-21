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
    /// The action a <see cref="StreamingResponseBase"/> should take when a send hook throws.
    /// Returned by <see cref="StreamingResponseBase.HandleSendErrorAsync(Exception, CancellationToken)"/>.
    /// </summary>
    public enum StreamErrorAction
    {
        /// <summary>
        /// Ignore the error and keep streaming (the failed send is dropped).
        /// </summary>
        Continue,

        /// <summary>
        /// Stop streaming intermediate updates for the remainder of the turn, but continue
        /// to send a final (non-streamed) message.  The base sets <see cref="StreamingResponseBase.IsStreamingChannel"/>
        /// to <c>false</c> and stops the stream loop; <see cref="StreamingResponseBase.FinalizeStreamAsync(bool, CancellationToken)"/> is still called.
        /// </summary>
        FallbackToNonStreaming,

        /// <summary>
        /// Stop the stream.  The result of <see cref="StreamingResponseBase.EndStreamAsync(CancellationToken)"/> will be
        /// <see cref="StreamingResponseResult.UserCancelled"/> when <see cref="StreamingResponseBase.UserCancelledStream"/>
        /// is set (by the subclass before returning), otherwise <see cref="StreamingResponseResult.Error"/>.
        /// </summary>
        Cancel,
    }

    /// <summary>
    /// Provides the shared streaming infrastructure used by <see cref="IStreamingResponse"/> implementations:
    /// a text buffer, an interval send loop, sequence tracking, and buffered/informative send scheduling.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Channel extension authors extend this class to provide custom streaming behavior for a channel and
    /// register an <see cref="IStreamingResponseFactory"/> so the adapter can select it per channel.  The
    /// default <see cref="StreamingResponse"/> (Teams / WebChat / DirectLine) also extends this class.
    /// </para>
    /// <para>
    /// The four abstract hooks (<see cref="SendChunkAsync"/>, <see cref="SendInformativeAsync"/>,
    /// <see cref="FinalizeStreamAsync"/>, <see cref="HandleSendErrorAsync"/>) are the only channel-specific
    /// surface; everything else (buffering, interval loop, sequence numbers, cancellation, end/reset) is shared.
    /// </para>
    /// </remarks>
    public abstract class StreamingResponseBase : IStreamingResponse
    {
        /// <summary>
        /// The default time, in milliseconds, that <see cref="EndStreamAsync"/> will wait for the buffer to drain.
        /// </summary>
        public static readonly int DefaultEndStreamTimeout = (int)TimeSpan.FromMinutes(2).TotalMilliseconds;

        // A single pending send: either an informative update, or a buffered text chunk snapshot.
        private sealed class PendingSend
        {
            public bool IsInformative;
            public string Text;
        }

        private int _nextSequence = 1;
        private bool _ended;
        private CancellationTokenSource _streamCts;
        private bool _streamStarted;
        private bool _messageUpdated;
        private bool _canceled;
        private bool _userCanceled;

        // Ordered FIFO of pending sends (informative updates interleaved with buffered text chunks).
        private readonly List<PendingSend> _queue = [];
        private readonly AutoResetEvent _queueEmpty = new(false);

        private int _interval;
        private int _initialDelay = 250;

        /// <inheritdoc/>
        public int Interval
        {
            get => _interval;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Interval must be greater than or equal to 0.");
                _interval = value;
            }
        }

        /// <inheritdoc/>
        public int InitialDelay
        {
            get => _initialDelay;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "InitialDelay must be greater than or equal to 0.");
                _initialDelay = value;
            }
        }

        /// <inheritdoc/>
        public int EndStreamTimeout { get; set; } = DefaultEndStreamTimeout;

        /// <inheritdoc/>
        public bool IsStreamingChannel { get; protected set; }

        /// <inheritdoc/>
        /// <remarks>
        /// StreamId assignment is the subclass's responsibility (via the protected setter).  The base only
        /// resets it to <c>null</c> in <see cref="ResetAsync"/>.  Teams assigns it from the first send response;
        /// other channels typically assign a GUID up front.
        /// </remarks>
        public string StreamId { get; protected set; }

        /// <inheritdoc/>
        public string Message { get; protected set; } = "";

        /// <inheritdoc/>
        public IActivity FinalMessage { get; set; }

        /// <inheritdoc/>
        public bool FeedbackLoopEnabled { get; set; }

        /// <inheritdoc/>
        public string FeedbackLoopType { get; set; } = "default";

        /// <inheritdoc/>
        public bool? EnableGeneratedByAILabel { get; set; } = false;

        /// <inheritdoc/>
        public SensitivityUsageInfo? SensitivityLabel { get; set; }

        /// <inheritdoc/>
        public List<ClientCitation>? Citations { get; protected set; } = [];

        /// <summary>
        /// Attachments to be included in the final message.  Only used for the final message, not intermediate messages.
        /// </summary>
        public List<Attachment>? Attachments { get; protected set; } = [];

        /// <summary>
        /// When <see cref="HandleSendErrorAsync"/> returns <see cref="StreamErrorAction.Cancel"/>, set this to
        /// <c>true</c> to report the cancellation as <see cref="StreamingResponseResult.UserCancelled"/> rather
        /// than <see cref="StreamingResponseResult.Error"/>.
        /// </summary>
        protected bool UserCancelledStream { get; set; }

        /// <inheritdoc/>
        public int UpdatesSent() => _nextSequence - 1;

        /// <inheritdoc/>
        public bool IsStreamStarted() => _streamStarted;

        /// <inheritdoc/>
        public void AddAttachment(Attachment attachment)
        {
            AssertionHelpers.ThrowIfNull(attachment, nameof(attachment));
            Attachments ??= [];
            Attachments.Add(attachment);
        }

        /// <inheritdoc/>
        public void AddCitation(ClientCitation citation)
        {
            Citations ??= [];
            Citations.Add(citation);
        }

        /// <inheritdoc/>
        public void AddCitation(Citation citation, int citationPosition)
        {
            Citations ??= [];
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
        public void AddCitations(IList<Citation> citations)
        {
            if (citations.Count > 0)
            {
                Citations ??= [];

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
        public void AddCitations(IList<ClientCitation> citations)
        {
            if (citations.Count > 0)
            {
                Citations ??= [];
                Citations.AddRange(citations);
            }
        }

        /// <inheritdoc/>
        public async Task QueueInformativeUpdateAsync(string text, CancellationToken cancellationToken = default)
        {
            if (!IsStreamingChannel)
            {
                return;
            }

            lock (this)
            {
                if (_ended)
                {
                    throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.StreamingResponseEnded, null);
                }

                if (IsStreamStarted())
                {
                    // Stream already running - queue the informative so it is sent, in order, on the streaming loop.
                    _queue.Add(new PendingSend { IsInformative = true, Text = text });
                    _queueEmpty.Reset();
                    return;
                }
            }

            // Stream not started yet: send the first informative directly on the caller's thread, then start the loop.
            int sequence = _nextSequence++;
            try
            {
                await SendInformativeAsync(text, sequence, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await HandleErrorAsync(ex, cancellationToken).ConfigureAwait(false);
                return;
            }

            StartStream();
        }

        /// <inheritdoc/>
        public void QueueTextChunk(string text)
        {
            if (string.IsNullOrEmpty(text) || _canceled)
            {
                return;
            }

            lock (this)
            {
                if (_ended)
                {
                    throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.StreamingResponseEnded, null);
                }

                // Buffer all chunks.
                Message += text;
                Message = TransformBufferedTextSafe(Message);

                _messageUpdated = true;

                // Start the stream if needed.  The InitialDelay gives a quicker first message (better UX) when
                // no Informative update preceded it; the normal Interval is used after the first message.
                StartStream(InitialDelay);
            }
        }

        /// <inheritdoc/>
        public async Task<StreamingResponseResult> EndStreamAsync(CancellationToken cancellationToken = default)
        {
            if (!IsStreamingChannel)
            {
                lock (this)
                {
                    if (_ended)
                    {
                        return StreamingResponseResult.AlreadyEnded;
                    }

                    _ended = true;
                }

                // The loop isn't running for non-streaming channels.  Let the subclass send the buffered final message.
                await FinalizeStreamAsync(streamedPath: false, cancellationToken).ConfigureAwait(false);

                return StreamingResponseResult.Success;
            }
            else
            {
                lock (this)
                {
                    if (_ended)
                    {
                        return StreamingResponseResult.AlreadyEnded;
                    }

                    _ended = true;

                    if (_canceled)
                    {
                        return _userCanceled ? StreamingResponseResult.UserCancelled : StreamingResponseResult.Error;
                    }

                    if (!IsStreamStarted())
                    {
                        return StreamingResponseResult.NotStarted;
                    }
                }

                StreamingResponseResult result = StreamingResponseResult.Success;

                // Wait for queued items to be sent on the streaming loop interval.
                try
                {
                    if (!_queueEmpty.WaitOne(EndStreamTimeout))
                    {
                        result = StreamingResponseResult.Timeout;
                    }

                    if (_canceled)
                    {
                        return _userCanceled ? StreamingResponseResult.UserCancelled : StreamingResponseResult.Error;
                    }
                }
                catch (AbandonedMutexException)
                {
                    StopStream();
                }

                // A fallback (see StreamErrorAction.FallbackToNonStreaming) may have flipped IsStreamingChannel
                // to false while draining; FinalizeStreamAsync inspects IsStreamingChannel to decide how to send.
                await FinalizeStreamAsync(streamedPath: true, cancellationToken).ConfigureAwait(false);

                return result;
            }
        }

        /// <inheritdoc/>
        public async Task ResetAsync(CancellationToken cancellationToken = default)
        {
            if (IsStreamStarted())
            {
                await EndStreamAsync(cancellationToken).ConfigureAwait(false);
            }

            lock (this)
            {
                StopStream();
                _ended = false;
                _queue.Clear();
                _messageUpdated = false;
                _nextSequence = 1;
                StreamId = null;
                _canceled = false;
                _userCanceled = false;
                UserCancelledStream = false;
                Message = "";
                FinalMessage = null;
                Citations = [];
                Attachments = [];
                SensitivityLabel = null;
                EnableGeneratedByAILabel = false;

                OnReset();
            }
        }

        /// <summary>
        /// Sends a chunk of accumulated (buffered) text as a channel-specific intermediate message.
        /// Called on the streaming loop.
        /// </summary>
        /// <param name="bufferedText">The full accumulated message text at this point in the stream.</param>
        /// <param name="sequenceNumber">The 1-based sequence number for this update.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        protected abstract Task SendChunkAsync(string bufferedText, int sequenceNumber, CancellationToken cancellationToken);

        /// <summary>
        /// Sends an informative ("thinking") update.  Called on the caller's thread for the first update, and on
        /// the streaming loop thereafter.  Implement as a no-op if the channel does not support informative updates.
        /// </summary>
        /// <param name="text">The informative text.</param>
        /// <param name="sequenceNumber">The 1-based sequence number for this update.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        protected abstract Task SendInformativeAsync(string text, int sequenceNumber, CancellationToken cancellationToken);

        /// <summary>
        /// Sends the final message and/or closes the channel stream.  Called from <see cref="EndStreamAsync"/> after
        /// the buffer has drained (or immediately for non-streaming channels).
        /// </summary>
        /// <param name="streamedPath">
        /// <c>true</c> when <see cref="EndStreamAsync"/> took the streaming (loop) path (i.e. the channel was
        /// a streaming channel when the stream ended), <c>false</c> for a non-streaming channel.  A
        /// <see cref="StreamErrorAction.FallbackToNonStreaming"/> can set <see cref="IsStreamingChannel"/> to
        /// <c>false</c> while <paramref name="streamedPath"/> remains <c>true</c>.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        protected abstract Task FinalizeStreamAsync(bool streamedPath, CancellationToken cancellationToken);

        /// <summary>
        /// Interprets a send error and returns the action the base should apply.  Called on the streaming loop
        /// (or the caller's thread for the first informative update).
        /// </summary>
        /// <param name="exception">The exception thrown by a send hook.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        protected abstract Task<StreamErrorAction> HandleSendErrorAsync(Exception exception, CancellationToken cancellationToken);

        /// <summary>
        /// Transforms the buffered text after each chunk is appended.  Default is identity.  The default
        /// <see cref="StreamingResponse"/> overrides this to normalize citation markers.
        /// </summary>
        protected virtual string TransformBufferedText(string bufferedText) => bufferedText;

        /// <summary>
        /// Called (under lock) at the end of <see cref="ResetAsync"/> so subclasses can reset their own state.
        /// </summary>
        protected virtual void OnReset()
        {
        }

        private string TransformBufferedTextSafe(string text) => TransformBufferedText(text) ?? text;

        private void StartStream(int interval = 0)
        {
            lock (this)
            {
                if (_streamStarted || !IsStreamingChannel)
                {
                    return;
                }

                _streamStarted = true;
                var cts = new CancellationTokenSource();
                _streamCts = cts;
                int dueTime = interval == 0 ? Interval : interval;

                // Fire-and-forget: the loop yields immediately at the first Task.Delay.
                _ = RunStreamAsync(dueTime, cts);
            }
        }

        private void StopStream()
        {
            _streamStarted = false;
            var cts = _streamCts;
            _streamCts = null;
            if (cts != null)
            {
                try
                {
                    cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // The owning loop already disposed the source while exiting.
                }
            }
        }

        private void QueueNextChunk()
        {
            if (!_messageUpdated)
            {
                return;
            }
            _messageUpdated = false;

            // Snapshot the current accumulated text for this chunk.
            _queue.Add(new PendingSend { IsInformative = false, Text = Message });
            _queueEmpty.Reset();
        }

        // Background loop that replaces the previous interval Timer.  Like TypingWorker, it uses
        // Task.Delay instead of System.Threading.Timer, avoiding thread-pool timer callbacks and
        // self-rescheduling shared timer state.  Each iteration waits the current due time, then sends one
        // buffered/informative item; the tick returns the next due time (or a negative value to stop).
        private async Task RunStreamAsync(int firstDueTime, CancellationTokenSource cts)
        {
            var stopToken = cts.Token;
            try
            {
                int dueTime = firstDueTime;
                while (true)
                {
                    await Task.Delay(dueTime, stopToken).ConfigureAwait(false);

                    int nextDueTime = await SendNextIntervalAsync().ConfigureAwait(false);
                    if (nextDueTime < 0)
                    {
                        return;
                    }

                    dueTime = nextDueTime;
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when the stream is stopped (cancel / fallback / reset).
            }
            catch (Exception)
            {
                // Best-effort: a failure in the streaming loop must not crash the process or fault the turn.
            }
            finally
            {
                cts.Dispose();
            }
        }

        // Sends one buffered chunk (or informative) per interval and returns the next due time in milliseconds,
        // or a negative value to stop the loop.  Mirrors the previous self-rescheduling Timer callback.
        private async Task<int> SendNextIntervalAsync()
        {
            PendingSend pending;

            lock (this)
            {
                QueueNextChunk();

                if (_queue.Count > 0)
                {
                    pending = _queue[0];
                    _queue.RemoveAt(0);
                }
                else if (_ended)
                {
                    _queueEmpty.Set();
                    StopStream();
                    return -1;
                }
                else
                {
                    // Nothing queued and not ending - chunking is likely slow.  Poll faster to pick up the next chunk.
                    return 200;
                }
            }

            // Can't await inside the lock.
            try
            {
                int sequence = _nextSequence++;
                if (pending.IsInformative)
                {
                    await SendInformativeAsync(pending.Text, sequence, CancellationToken.None).ConfigureAwait(false);
                }
                else
                {
                    await SendChunkAsync(pending.Text, sequence, CancellationToken.None).ConfigureAwait(false);
                }

                // Continue on the normal interval.
                return Interval;
            }
            catch (Exception ex)
            {
                return await HandleErrorAsync(ex, CancellationToken.None).ConfigureAwait(false);
            }
        }

        private async Task<int> HandleErrorAsync(Exception exception, CancellationToken cancellationToken)
        {
            // Errors are handled here (not rethrown) since this is running on the streaming loop and would
            // otherwise fault the loop task.
            UserCancelledStream = false;
            StreamErrorAction action;
            try
            {
                action = await HandleSendErrorAsync(exception, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                action = StreamErrorAction.Cancel;
            }

            lock (this)
            {
                switch (action)
                {
                    case StreamErrorAction.Continue:
                        // Ignore the failed send and keep streaming on the normal interval.
                        return Interval;

                    case StreamErrorAction.FallbackToNonStreaming:
                        // Disable streaming for the rest of the turn; a plain final message will still be sent.
                        IsStreamingChannel = false;
                        StopStream();
                        _queueEmpty.Set();
                        return -1;

                    case StreamErrorAction.Cancel:
                    default:
                        StopStream();
                        _canceled = true;
                        _userCanceled = UserCancelledStream;
                        _queueEmpty.Set();
                        return -1;
                }
            }
        }
    }
}
