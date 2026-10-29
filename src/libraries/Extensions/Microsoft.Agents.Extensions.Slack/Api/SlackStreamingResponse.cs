// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Slack.Api;

/// <summary>
/// Implements native Slack streaming, including source links and supported attachment-to-Block-Kit mappings.
/// </summary>
internal sealed class SlackStreamingResponse : IStreamingResponse
{
    private sealed class Informative(string text, int textPosition, CancellationToken cancellationToken)
    {
        internal readonly string Text = text;
        internal readonly int TextPosition = textPosition;
        internal readonly CancellationToken CancellationToken = cancellationToken;
        internal readonly TaskCompletionSource<bool> Sent = NewSignal();
    }

    private static readonly TimeSpan StreamLifetime = TimeSpan.FromMinutes(9);
    private readonly object _sync = new();
    private readonly ITurnContext _context;
    private readonly SlackApi _api;
    private readonly StringBuilder _text = new();
    private readonly List<object> _attachments = [];
    private readonly Queue<Informative> _informative = new();
    private CancellationTokenSource _workerCts = new();
    private TaskCompletionSource<bool> _signal = NewSignal();
    private Task _worker = Task.CompletedTask;
    private Task<StreamingResponseResult> _ending;
    private SlackStream _stream;
    private DateTimeOffset _streamStarted;
    private string _taskId = Guid.NewGuid().ToString();
    private string _carry = "";
    private int _sentLength;
    private int _segmentLength;
    private int _updates;
    private bool _hasTask;
    private bool _started;
    private bool _endRequested;
    private bool _ended;
    private bool _ambiguousSend;
    private Exception _failure;
    private List<object> _finalBlocks;
    private int _finalBlockOffset;

    internal SlackStreamingResponse(ITurnContext turnContext, SlackApi slackApi)
    {
        AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
        AssertionHelpers.ThrowIfNull(slackApi, nameof(slackApi));
        _context = turnContext;
        _api = slackApi;
    }

    [Obsolete("FinalMessage is Activity Protocol-specific. Use AddAttachment and AddCitation instead.")]
    public IActivity FinalMessage
    {
        get => null;
        set => throw new NotSupportedException("Slack streaming uses AddAttachment and AddCitation, not FinalMessage.");
    }

    public int Interval { get; set; }
    public int InitialDelay { get; set; }
    public int EndStreamTimeout { get; set; } = 120000;
    public bool IsStreamingChannel => true;
    public string StreamId => _stream?.MessageTs;
    public string Message { get { lock (_sync) { return _text.ToString() + _carry; } } }
    public bool FeedbackLoopEnabled { get; set; }
    public string FeedbackLoopType { get; set; } = "default";
    public bool? EnableGeneratedByAILabel { get; set; } = false;
    public SensitivityUsageInfo? SensitivityLabel { get; set; }
    public List<ClientCitation> Citations { get; } = [];
    public string StreamingTakingTooLongMessage { get; set; } =
        "The response is taking longer than expected. Please wait while we continue to generate the response.";

    public bool IsStreamStarted() { lock (_sync) { return _started && !_ended; } }
    public int UpdatesSent() { lock (_sync) { return _updates; } }

    public void AddAttachment(Attachment attachment)
    {
        var blocks = SlackStreamingContent.AttachmentBlocks(attachment);
        foreach (var block in blocks)
        {
            if (MarkdownLength(block) > 12000)
            {
                throw new NotSupportedException("The attachment exceeds Slack's markdown block limit.");
            }
        }
        lock (_sync)
        {
            ThrowIfEnding();
            _attachments.AddRange(blocks);
            _started = true;
        }
    }

    public void AddCitation(ClientCitation citation)
    {
        AssertionHelpers.ThrowIfNull(citation, nameof(citation));
        lock (_sync)
        {
            ThrowIfEnding();
            Citations.Add(citation);
            _started = true;
        }
    }

    public void AddCitation(Citation citation, int citationPosition)
    {
        AssertionHelpers.ThrowIfNull(citation, nameof(citation));
        AddCitation(new ClientCitation
        {
            Position = citationPosition,
            Appearance = new ClientCitationAppearance
            {
                Name = citation.Title ?? $"Document #{citationPosition}",
                Abstract = citation.Content == null ? null : CitationUtils.Snippet(citation.Content, 480),
                Url = citation.Url
            }
        });
    }

    public void AddCitations(IList<Citation> citations)
    {
        AssertionHelpers.ThrowIfNull(citations, nameof(citations));
        foreach (var citation in citations)
        {
            AddCitation(citation, Citations.Count + 1);
        }
    }

    public void AddCitations(IList<ClientCitation> citations)
    {
        AssertionHelpers.ThrowIfNull(citations, nameof(citations));
        foreach (var citation in citations)
        {
            AddCitation(citation);
        }
    }

    public void QueueTextChunk(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        lock (_sync)
        {
            ThrowIfEnding();
            if (_failure != null)
            {
                return;
            }
            AppendText(text, false);
            _started = true;
            StartWorker();
        }
    }

    public async Task QueueInformativeUpdateAsync(string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Informative informative;
        lock (_sync)
        {
            ThrowIfEnding();
            if (_failure != null)
            {
                return;
            }
            informative = new Informative(text, _text.Length, cancellationToken);
            _informative.Enqueue(informative);
            _started = true;
            StartWorker();
        }
        using var registration = cancellationToken.Register(() => informative.Sent.TrySetCanceled(cancellationToken));
        await informative.Sent.Task.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    public Task<StreamingResponseResult> EndStreamAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (_ended)
            {
                return Task.FromResult(StreamingResponseResult.AlreadyEnded);
            }
            if (_ending != null)
            {
                return _ending;
            }
            AppendText("", true);
            _endRequested = true;
            _signal.TrySetResult(true);
            return _ending = EndCoreAsync(cancellationToken);
        }
    }

    private async Task<StreamingResponseResult> EndCoreAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var timedOut = false;
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                var timeout = DelayAsync(
                    EndStreamTimeout == Timeout.Infinite ? Timeout.InfiniteTimeSpan : TimeSpan.FromMilliseconds(EndStreamTimeout),
                    deadline.Token);
                var completed = await Task.WhenAny(_worker, timeout).ConfigureAwait(false);
                if (completed != _worker)
                {
                    timedOut = !cancellationToken.IsCancellationRequested;
                    _workerCts.Cancel();
                }
                deadline.Cancel();
                await _worker.ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();

            var userCancelled = _failure is SlackResponseException { ErrorCode: "stopped_by_user" };
            if (userCancelled)
            {
                _stream = null;
            }
            else if (_failure != null || _ambiguousSend)
            {
                await CloseSegmentAsync(null, false, "error", cancellationToken).ConfigureAwait(false);
            }
            else
            {
                while (await SendPendingAsync(cancellationToken).ConfigureAwait(false))
                {
                }
                if (_finalBlocks == null)
                {
                    var blocks = new List<object>(_attachments);
                    blocks.AddRange(SlackStreamingContent.CitationBlocks(Message, Citations));
                    if (FeedbackLoopEnabled)
                    {
                        blocks.Add(SlackStreamingContent.FeedbackBlock());
                    }
                    _finalBlocks = blocks;
                }
                await SendFinalBlocksAsync(cancellationToken).ConfigureAwait(false);
            }

            lock (_sync)
            {
                _ended = true;
                _started = false;
            }
            return userCancelled ? StreamingResponseResult.UserCancelled
                : timedOut ? StreamingResponseResult.Timeout
                : _failure != null || _ambiguousSend ? StreamingResponseResult.Error
                : _updates == 0 && _finalBlocks?.Count == 0 ? StreamingResponseResult.NotStarted
                : StreamingResponseResult.Success;
        }
        catch (SlackResponseException exception) when (exception.ErrorCode == "stopped_by_user")
        {
            _stream = null;
            lock (_sync)
            {
                _failure = exception;
                _ended = true;
                _started = false;
            }
            return StreamingResponseResult.UserCancelled;
        }
        catch (SlackResponseException exception) when (
            exception.ErrorCode is "message_not_in_streaming_state" or "message_not_found")
        {
            _stream = null;
            lock (_sync)
            {
                _failure = exception;
                _ended = true;
                _started = false;
            }
            return StreamingResponseResult.Error;
        }
        catch
        {
            _workerCts.Cancel();
            await _worker.ConfigureAwait(false);
            lock (_sync)
            {
                _ending = null;
            }
            throw;
        }
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        if (IsStreamStarted() || _endRequested && !_ended)
        {
            await EndStreamAsync(cancellationToken).ConfigureAwait(false);
        }
        lock (_sync)
        {
            _workerCts.Dispose();
            _workerCts = new CancellationTokenSource();
            _worker = Task.CompletedTask;
            _ending = null;
            _signal = NewSignal();
            _text.Clear();
            _carry = "";
            _attachments.Clear();
            Citations.Clear();
            _informative.Clear();
            _stream = null;
            _taskId = Guid.NewGuid().ToString();
            _sentLength = _segmentLength = _updates = _finalBlockOffset = 0;
            _started = _ended = _endRequested = _ambiguousSend = _hasTask = false;
            _failure = null;
            _finalBlocks = null;
            FeedbackLoopEnabled = false;
            FeedbackLoopType = "default";
            EnableGeneratedByAILabel = false;
            SensitivityLabel = null;
            Interval = InitialDelay = 0;
            EndStreamTimeout = 120000;
            StreamingTakingTooLongMessage =
                "The response is taking longer than expected. Please wait while we continue to generate the response.";
        }
    }

    public Task<bool> SendStreamTimedOutNotification(string message, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    private void StartWorker()
    {
        _signal.TrySetResult(true);
        if (_worker.IsCompleted)
        {
            _worker = Task.Run(() => RunAsync(_workerCts.Token));
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_stream != null && _api.TimeProvider.GetUtcNow() - _streamStarted >= StreamLifetime)
                {
                    await CloseSegmentAsync(null, true, "complete", cancellationToken).ConfigureAwait(false);
                }
                if (await SendPendingAsync(cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }
                Task wake;
                lock (_sync)
                {
                    if (_endRequested)
                    {
                        return;
                    }
                    if (_informative.Count > 0 || _text.Length > _sentLength)
                    {
                        continue;
                    }
                    if (_signal.Task.IsCompleted)
                    {
                        _signal = NewSignal();
                    }
                    wake = _signal.Task;
                }
                using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var remaining = _stream == null ? Timeout.InfiniteTimeSpan
                    : StreamLifetime - (_api.TimeProvider.GetUtcNow() - _streamStarted);
                if (remaining < TimeSpan.Zero && remaining != Timeout.InfiniteTimeSpan)
                {
                    continue;
                }
                await Task.WhenAny(wake, DelayAsync(remaining, wait.Token)).ConfigureAwait(false);
                wait.Cancel();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            lock (_sync)
            {
                _failure = exception;
            }
            System.Diagnostics.Trace.TraceWarning("Slack streaming failed: {0}",
                exception is SlackResponseException error ? error.ErrorCode : exception.GetType().Name);
        }
        finally
        {
            lock (_sync)
            {
                foreach (var pending in _informative)
                {
                    pending.Sent.TrySetResult(false);
                }
            }
        }
    }

    private async Task<bool> SendPendingAsync(CancellationToken cancellationToken)
    {
        Informative informative = null;
        string text = null;
        lock (_sync)
        {
            var limit = _informative.Count > 0 ? _informative.Peek().TextPosition : _text.Length;
            if (limit > _sentLength)
            {
                var length = Math.Min(12000 - _segmentLength, limit - _sentLength);
                if (length > 0 && _sentLength + length < _text.Length
                    && char.IsHighSurrogate(_text[_sentLength + length - 1]))
                {
                    length--;
                }
                text = _text.ToString(_sentLength, length);
            }
            else if (_informative.Count > 0)
            {
                informative = _informative.Peek();
                if (informative.CancellationToken.IsCancellationRequested)
                {
                    _informative.Dequeue();
                    informative.Sent.TrySetCanceled(informative.CancellationToken);
                    return true;
                }
            }
            else
            {
                return false;
            }
        }

        if (text == "")
        {
            await CloseSegmentAsync(null, true, "complete", cancellationToken).ConfigureAwait(false);
            return true;
        }
        try
        {
            if (_stream != null && _api.TimeProvider.GetUtcNow() - _streamStarted >= StreamLifetime)
            {
                await CloseSegmentAsync(null, true, "complete", cancellationToken).ConfigureAwait(false);
                return true;
            }
            await EnsureStreamAsync(cancellationToken).ConfigureAwait(false);
            if (informative != null)
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, informative.CancellationToken);
                await _stream.AppendAsync(
                    [new TaskUpdateChunk(_taskId, informative.Text ?? "", SlackTaskStatus.InProgress)],
                    linked.Token).ConfigureAwait(false);
                _hasTask = true;
                lock (_sync)
                {
                    _informative.Dequeue();
                    informative.Sent.TrySetResult(true);
                    _updates++;
                }
            }
            else
            {
                await _stream.AppendAsync([new MarkdownTextChunk(text)], cancellationToken).ConfigureAwait(false);
                lock (_sync)
                {
                    _sentLength += text.Length;
                    _segmentLength += text.Length;
                    _updates++;
                }
            }
            return true;
        }
        catch (OperationCanceledException)
        {
            // Slack may have accepted the POST before the response was cancelled. Never replay it blindly.
            _ambiguousSend = true;
            throw;
        }
        catch (SlackResponseException exception) when (
            exception.ErrorCode == "message_not_in_streaming_state"
            && _api.TimeProvider.GetUtcNow() - _streamStarted >= StreamLifetime)
        {
            _stream = null;
            _segmentLength = 0;
            _hasTask = false;
            return true;
        }
        catch (SlackResponseException exception) when (exception.ErrorCode == "msg_too_long" && _segmentLength > 0)
        {
            await CloseSegmentAsync(null, true, "complete", cancellationToken).ConfigureAwait(false);
            return true;
        }
    }

    private async Task EnsureStreamAsync(CancellationToken cancellationToken)
    {
        if (_stream != null)
        {
            return;
        }
        var data = _context.Activity.GetChannelData<SlackChannelData>()
            ?? throw new InvalidOperationException("Slack streaming requires SlackChannelData.");
        var stream = new SlackStream(_api, data.Channel, data.ThreadTs, data.ApiToken,
            data.UserId, data.UserTeamId, data.TeamId == null ? null : $"{data.AppId}:{data.TeamId}");
        await stream.StartAsync(TaskDisplayMode.Plan, cancellationToken).ConfigureAwait(false);
        _stream = stream;
        _streamStarted = _api.TimeProvider.GetUtcNow();
        _segmentLength = 0;
        _hasTask = false;
    }

    private async Task SendFinalBlocksAsync(CancellationToken cancellationToken)
    {
        if (_stream == null && _finalBlocks.Count == 0)
        {
            return;
        }
        var expiredClosures = 0;
        do
        {
            await EnsureStreamAsync(cancellationToken).ConfigureAwait(false);
            var page = new List<object>();
            var markdownLength = _segmentLength;
            while (_finalBlockOffset + page.Count < _finalBlocks.Count && page.Count < 50)
            {
                var block = _finalBlocks[_finalBlockOffset + page.Count];
                var length = MarkdownLength(block);
                if (length > 12000)
                {
                    throw new NotSupportedException("A Slack markdown block exceeds 12000 characters.");
                }
                if (markdownLength + length > 12000)
                {
                    break;
                }
                page.Add(block);
                markdownLength += length;
            }
            var more = _finalBlockOffset + page.Count < _finalBlocks.Count;
            if (await CloseSegmentAsync(page, more, "complete", cancellationToken).ConfigureAwait(false))
            {
                _finalBlockOffset += page.Count;
                expiredClosures = 0;
            }
            else if (++expiredClosures >= 3)
            {
                _failure = new TimeoutException("Slack repeatedly expired before final blocks could be delivered.");
                System.Diagnostics.Trace.TraceWarning("Slack repeatedly expired during finalization.");
                return;
            }
        } while (_finalBlockOffset < _finalBlocks.Count);
    }

    private async Task<bool> CloseSegmentAsync(IList<object> blocks, bool continuing, string status, CancellationToken cancellationToken)
    {
        if (_stream == null)
        {
            return true;
        }
        var chunks = _hasTask
            ? new List<Chunk> { new TaskUpdateChunk(_taskId, status == "error" ? "Failed" : "Done", status) }
            : null;
        var delivered = true;
        try
        {
            await _stream.StopAsync(chunks, blocks, continuing ? "processing" : "active", cancellationToken).ConfigureAwait(false);
        }
        catch (SlackResponseException exception) when (exception.ErrorCode == "message_not_in_streaming_state"
            && _api.TimeProvider.GetUtcNow() - _streamStarted >= StreamLifetime)
        {
            delivered = false;
        }
        _stream = null;
        _segmentLength = 0;
        _hasTask = false;
        return delivered;
    }

    private void AppendText(string text, bool flush)
    {
        var combined = _carry + text;
        var length = combined.Length;
        if (!flush)
        {
            var opening = combined.LastIndexOf('[');
            if (opening >= 0)
            {
                var suffix = combined.Substring(opening + 1);
                if (suffix.Length <= 16 && ("doc".StartsWith(suffix, StringComparison.OrdinalIgnoreCase)
                    || "docs".StartsWith(suffix, StringComparison.OrdinalIgnoreCase)
                    || Regex.IsMatch(suffix, @"^docs?\d*$", RegexOptions.IgnoreCase)))
                {
                    length = opening;
                }
            }
            if (length > 0 && char.IsHighSurrogate(combined[length - 1]))
            {
                length--;
            }
        }
        _text.Append(CitationUtils.FormatCitationsResponse(combined.Substring(0, length)));
        _carry = combined.Substring(length);
    }

    private void ThrowIfEnding()
    {
        if (_endRequested || _ended)
        {
            throw new InvalidOperationException("The streaming response has ended. Call ResetAsync before reusing it.");
        }
    }

    private Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        return Task.Delay(delay, _api.TimeProvider, cancellationToken);
#else
        return _api.TimeProvider.Delay(delay, cancellationToken);
#endif
    }

    private static int MarkdownLength(object block)
    {
        var json = JsonSerializer.SerializeToElement(block);
        return json.TryGetProperty("type", out var type) && type.GetString() == "markdown"
            ? json.GetProperty("text").GetString().Length : 0;
    }

    private static TaskCompletionSource<bool> NewSignal()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
