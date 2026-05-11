// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Abstract base for streaming response implementations.
    /// Owns the text buffer, send loop, and stream state.
    /// Extension authors subclass this and implement the abstract channel-specific hooks.
    /// </summary>
    /// <remarks>
    /// This class support throttling via the <see cref="Interval"/> property.  Teams and Azure Channels require
    /// some throttling since services like OpenAI produce streams that exceed allowed Channel message limits.
    /// Teams defaults to 1000ms per intermediate message, and WebChat 500ms.  Reducing the Interval could result
    /// in message delivery failures.
    /// </remarks>
    public abstract class StreamingResponseBase : IStreamingResponse
    {
        public static readonly int DefaultEndStreamTimeout = (int)TimeSpan.FromMinutes(2).TotalMilliseconds;

        private CancellationTokenSource? _cts;
        private Task? _loopTask;
        private bool _disposed;
        private bool _messageUpdated;
        private bool _streamStarted;
        private bool _streamEnded;
        private bool _streamCancelled;
        private bool _userCancelled;
        private readonly object _lock = new();
        private int _sequenceNumber;
        private int _initialDelay = 250;


        /// <summary>Gets or sets the interval in ms between intermediate sends.</summary>
        public int Interval { get; set; } = 500;

        /// <summary>
        /// The initial delay in milliseconds before the first intermediate message is sent.
        /// Defaults to 250. Set to a small value in tests.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a negative value.</exception>
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

        /// <summary>Gets the accumulated message text (citation-formatted).</summary>
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

        /// <inheritdoc/>
        public virtual IActivity FinalMessage { get; set; }

        /// <inheritdoc/>
        public virtual bool? EnableGeneratedByAILabel { get; set; } = false;

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
                {
                    if (_streamCancelled)
                        return;
                    throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(
                        ErrorHelper.StreamingResponseEnded, null);
                }

                Message += text;
                Message = CitationUtils.FormatCitationsResponse(Message);
                _messageUpdated = true;
                StartStream(InitialDelay);
            }
        }

        /// <inheritdoc/>
        public async Task QueueInformativeUpdateAsync(string text, CancellationToken cancellationToken = default)
        {
            if (!IsStreamingChannel)
                return;

            int seq;
            lock (_lock)
            {
                if (_streamEnded)
                    throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(
                        ErrorHelper.StreamingResponseEnded, null);

                seq = ++_sequenceNumber;
            }

            await SendInformativeAsync(text, seq, cancellationToken).ConfigureAwait(false);

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
                Task? loopTask;
                lock (_lock)
                {
                    if (_streamEnded)
                        return StreamingResponseResult.AlreadyEnded;
                    _streamEnded = true;

                    if (_streamCancelled)
                        return _userCancelled ? StreamingResponseResult.UserCancelled : StreamingResponseResult.Error;

                    if (!_streamStarted)
                        return StreamingResponseResult.NotStarted;

                    loopTask = _loopTask;
                }

                // Wait for the send loop to drain the buffer
                if (loopTask != null)
                {
                    var timeoutTask = Task.Delay(EndStreamTimeout, cancellationToken);
                    var completed = await Task.WhenAny(loopTask, timeoutTask).ConfigureAwait(false);
                    if (completed != loopTask)
                    {
                        StopStream();
                        cancellationToken.ThrowIfCancellationRequested();
                        return StreamingResponseResult.Timeout;
                    }
                }

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
                FinalMessage = null;
                SensitivityLabel = null;
                EnableGeneratedByAILabel = false;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases resources owned by this instance.
        /// Subclasses that hold additional disposable resources should override this method,
        /// call base.Dispose(disposing), and dispose their own resources when disposing is true.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    StopStream();
                }
                _disposed = true;
            }
        }

        // ── Abstract hooks ────────────────────────────────────────────────────

        /// <summary>
        /// Called by the send loop with accumulated text. Send as a channel-specific intermediate message.
        /// </summary>
        protected abstract Task SendChunksAsync(string bufferedText, int sequenceNumber, CancellationToken cancellationToken);

        /// <summary>
        /// Called on the caller's thread when QueueInformativeUpdateAsync is called on a streaming channel.
        /// Send a "thinking/working" update to the channel. Implement as no-op if unsupported.
        /// </summary>
        protected abstract Task SendInformativeAsync(string text, int sequenceNumber, CancellationToken cancellationToken);

        /// <summary>
        /// Called after the buffer drains (streaming) or immediately (non-streaming) by EndStreamAsync.
        /// Send the final message, call channel stop API, or no-op.
        /// For FallbackToNonStreaming: IsStreamingChannel will be false — check it to decide format.
        /// </summary>
        protected abstract Task FinalizeStreamAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Called on the send loop when SendChunksAsync throws. Interpret the error and return
        /// a StreamErrorAction. The base class applies the action.
        /// </summary>
        protected abstract Task<StreamErrorAction> HandleSendErrorAsync(Exception ex, CancellationToken cancellationToken);

        // ── Send loop management ──────────────────────────────────────────────

        private void StartStream(int initialDelay = 0)
        {
            if (_loopTask == null && IsStreamingChannel)
            {
                _streamStarted = true;
                _cts = new CancellationTokenSource();
                _loopTask = RunSendLoopAsync(initialDelay == 0 ? Interval : initialDelay, _cts.Token);
            }
        }

        private void StopStream()
        {
            var loopTask = _loopTask;
            _loopTask = null;
            _cts?.Cancel();

            if (loopTask != null)
            {
                try
                {
                    loopTask.GetAwaiter().GetResult();
                }
                catch
                {
                    // Loop handles its own exceptions; swallow anything residual
                }
            }

            _cts?.Dispose();
            _cts = null;
        }

        private async Task RunSendLoopAsync(int initialDelay, CancellationToken ct)
        {
            try
            {
                await Task.Delay(initialDelay, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            while (!ct.IsCancellationRequested)
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
                    }
                    else if (_streamEnded)
                    {
                        return;
                    }
                }

                if (textToSend != null)
                {
                    try
                    {
                        await SendChunksAsync(textToSend, seqToSend, ct).ConfigureAwait(false);

                        lock (_lock)
                        {
                            if (_streamEnded && !_messageUpdated)
                                return;
                        }

                        await Task.Delay(Interval, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
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
                                case StreamErrorAction.Error:
                                    _streamCancelled = true;
                                    break;
                            }
                        }

                        if (action != StreamErrorAction.Continue)
                            return;

                        // Continue: retry after interval
                        try
                        {
                            await Task.Delay(Interval, ct).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                    }
                }
                else
                {
                    // No new text yet — poll faster while waiting for chunks
                    try
                    {
                        await Task.Delay(200, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            }
        }
    }
}
