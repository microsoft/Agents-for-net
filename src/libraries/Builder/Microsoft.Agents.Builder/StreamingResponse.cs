// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// This class enables sending chunked text in a series of "intermediate" messages, on an interval.  This
    /// gives the UX of a streamed message. When complete, a final message (ActivityType.Message) is sent with
    /// the full message and optional Attachments.
    ///
    /// The expected sequence of calls is:
    ///
    /// `QueueInformativeUpdateAsync()`, `QueueTextChunk()`, `QueueTextChunk()`, ..., `EndStreamAsync()`.
    ///
    ///  Once `EndStreamAsync()` is called, the stream is considered ended and no further updates can be sent.
    /// </summary>
    /// <remarks>
    /// Only Teams and WebChat support streaming messages.  However, channels that do not support
    /// streaming messages will only receive the final message when <see cref="EndStreamAsync"/> is called.
    /// </remarks>
    internal class StreamingResponse : StreamingResponseBase
    {
        private const string TeamsStreamCancelled = "ContentStreamNotAllowed";
        private const string BadArgument = "BadArgument";
        private const string TeamsStreamNotAllowed = "streaming api is not enabled";

        private readonly TurnContext _context;
        private bool _isTeamsChannel;

        /// <summary>
        /// Creates a new instance of the <see cref="StreamingResponse"/> class.
        /// </summary>
        /// <param name="turnContext">Context for the current turn of conversation with the user.</param>
        public StreamingResponse(TurnContext turnContext)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));

            _context = turnContext;
            SetDefaults(turnContext);
        }

        /// <summary>
        /// Reset an already used stream.  If the stream is still running, this will wait for completion.
        /// </summary>
        public override async Task ResetAsync(CancellationToken cancellationToken = default)
        {
            await base.ResetAsync(cancellationToken).ConfigureAwait(false);
            SetDefaults(_context);
        }

        // ── Abstract hook implementations ─────────────────────────────────────

        protected override async Task SendChunksAsync(string bufferedText, int sequenceNumber, CancellationToken ct)
        {
            var activity = new Activity
            {
                Type = ActivityTypes.Typing,
                Text = bufferedText,
                Entities = [new StreamInfo
                {
                    StreamType = StreamTypes.Streaming,
                    StreamSequence = sequenceNumber,
                }]
            };

            if (Citations is { Count: > 0 })
            {
                var currCitations = CitationUtils.GetUsedCitations(bufferedText, Citations);
                var entity = new AIEntity();
                if (currCitations is { Count: > 0 })
                    entity.Citation = currCitations;
                activity.Entities.Add(entity);
            }

            await SendStreamActivityAsync(activity, ct).ConfigureAwait(false);
        }

        protected override async Task SendInformativeAsync(string text, int sequenceNumber, CancellationToken ct)
        {
            var activity = new Activity
            {
                Type = ActivityTypes.Typing,
                Text = text,
                Entities = [new StreamInfo { StreamType = StreamTypes.Informative, StreamSequence = sequenceNumber }]
            };
            await SendStreamActivityAsync(activity, ct).ConfigureAwait(false);
        }

        protected override Task FinalizeStreamAsync(CancellationToken ct)
        {
            if (!IsStreamingChannel)
            {
                // Non-streaming or fallback: send plain message only if there's content
                if (UpdatesSent() > 0 || FinalMessage != null || !string.IsNullOrWhiteSpace(Message))
                    return _context.SendActivityAsync(CreateFinalMessage(), ct);
                return Task.CompletedTask;
            }

            if (UpdatesSent() > 0 || FinalMessage != null)
                return SendStreamActivityAsync(CreateFinalMessage(), ct);
            return Task.CompletedTask;
        }

        protected override Task<StreamErrorAction> HandleSendErrorAsync(Exception ex, CancellationToken ct)
        {
            if (ex is ErrorResponseException errorResponse)
            {
                if (TeamsStreamCancelled.Equals(errorResponse.Body?.Error?.Code, StringComparison.OrdinalIgnoreCase))
                {
                    _context?.Adapter?.Logger?.LogWarning("User canceled stream on the client side.");
                    return Task.FromResult(StreamErrorAction.Cancel);
                }

                if (BadArgument.Equals(errorResponse.Body?.Error?.Code, StringComparison.OrdinalIgnoreCase) &&
                    errorResponse.Body?.Error?.Message?.IndexOf(TeamsStreamNotAllowed, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _context?.Adapter?.Logger?.LogWarning("Interaction Context does not support StreamingResponse, StreamingResponse has been disabled for this turn");
                    return Task.FromResult(StreamErrorAction.FallbackToNonStreaming);
                }

                var errorMessage = errorResponse.Body?.Error?.Message ?? "None";
                _context?.Adapter?.Logger?.LogWarning(
                    "Exception during StreamingResponse: {ExceptionMessage} - {ErrorMessage}",
                    ex.Message,
                    errorMessage);

                return Task.FromResult(StreamErrorAction.Error);
            }

            _context?.Adapter?.Logger?.LogWarning(
                "Unexpected exception during StreamingResponse: {ExceptionMessage}",
                ex.Message);
            return Task.FromResult(StreamErrorAction.Error);
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private async Task SendStreamActivityAsync(IActivity activity, CancellationToken ct)
        {
            if (!string.IsNullOrEmpty(StreamId))
            {
                activity.Id = StreamId;
                activity.GetStreamingEntity().StreamId = StreamId;
            }

            var response = await _context.SendActivityAsync(activity, ct).ConfigureAwait(false);

            if (string.IsNullOrEmpty(StreamId))
                StreamId = response.Id;
        }

        private void SetDefaults(TurnContext turnContext)
        {
            _isTeamsChannel = Channels.Msteams == turnContext.Activity.ChannelId?.Channel;

            if (string.Equals(DeliveryModes.ExpectReplies, turnContext.Activity.DeliveryMode, StringComparison.OrdinalIgnoreCase))
            {
                IsStreamingChannel = false;
            }
            else if (_isTeamsChannel)
            {
                Interval = 1000;
                IsStreamingChannel = true;
            }
            else if (Channels.Webchat == turnContext.Activity.ChannelId?.Channel || Channels.Directline == turnContext.Activity.ChannelId?.Channel)
            {
                Interval = 500;
                IsStreamingChannel = true;
                StreamId = Guid.NewGuid().ToString();
            }
            else if (string.Equals(DeliveryModes.Stream, turnContext.Activity.DeliveryMode, StringComparison.OrdinalIgnoreCase))
            {
                IsStreamingChannel = true;
                Interval = 100;
                StreamId = Guid.NewGuid().ToString();
            }
            else
            {
                IsStreamingChannel = false;
            }
        }

        private IActivity CreateFinalMessage()
        {
            var activity = FinalMessage ?? new Activity();

            activity.Type = ActivityTypes.Message;
            activity.Entities ??= [];
            if (FinalMessage == null)
            {
                activity.Text = !string.IsNullOrEmpty(Message) ? Message : "No text was streamed";
            }

            foreach (var existing in activity.Entities.Where(e => string.Equals(EntityTypes.StreamInfo, e.Type, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                activity.Entities.Remove(existing);
            }

            if (IsStreamingChannel)
            {
                activity.Entities.Add(new StreamInfo() { StreamType = StreamTypes.Final, StreamResult = (string.IsNullOrEmpty(Message) ? StreamResults.Error : StreamResults.Success) });
            }

            if (FeedbackLoopEnabled && _isTeamsChannel)
            {
                activity.ChannelData = ObjectPath.Merge(activity.ChannelData, new
                {
                    feedbackLoop = new
                    {
                        type = FeedbackLoopType ?? "default"
                    }
                });
            }

            List<ClientCitation>? currCitations = CitationUtils.GetUsedCitations(Message, Citations);
            if (EnableGeneratedByAILabel == true || currCitations != null)
            {
                AIEntity entity = new()
                {
                    Citation = currCitations,
                    UsageInfo = SensitivityLabel
                };

                if (EnableGeneratedByAILabel == true)
                {
                    entity.AdditionalType.Add(AIEntity.AdditionalTypeAIGeneratedContent);
                }

                activity.Entities.Add(entity);
            }

            return activity;
        }
    }
}
