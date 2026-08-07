// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Builder.State;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Streams Activity Protocol responses for Teams, WebChat, DirectLine, and DeliveryMode.Stream.
    /// </summary>
    internal class StreamingResponse : StreamingResponseBase
    {
        private const string TeamsStreamCancelled = "ContentStreamNotAllowed";
        private const string TeamsStreamTimedOut = "Content stream finished due to exceeded streaming time.";
        private const string BadArgument = "BadArgument";
        private const string TeamsStreamNotAllowed = "streaming api is not enabled";

        private readonly TurnContext _context;
        private bool _isTeamsChannel;
        private bool _streamTimedOut;

        public StreamingResponse(TurnContext turnContext)
        {
            Core.AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));

            _context = turnContext;
            SetDefaults(turnContext);
        }

        protected override string TransformBufferedText(string bufferedText)
        {
            return CitationUtils.FormatCitationsResponse(bufferedText);
        }

        protected override async Task SendChunkAsync(string bufferedText, int sequenceNumber, CancellationToken cancellationToken)
        {
            var activity = new Activity
            {
                Type = ActivityTypes.Typing,
                Text = bufferedText,
                Entities =
                [
                    new StreamInfo
                    {
                        StreamType = StreamTypes.Streaming,
                        StreamSequence = sequenceNumber,
                    }
                ]
            };

            if (Citations != null && Citations.Count > 0)
            {
                List<ClientCitation>? currentCitations = CitationUtils.GetUsedCitations(bufferedText, Citations);
                AIEntity entity = new();
                if (currentCitations != null && currentCitations.Count > 0)
                {
                    entity.Citation = currentCitations;
                }

                activity.Entities.Add(entity);
            }

            await SendStreamActivityAsync(activity, cancellationToken).ConfigureAwait(false);
        }

        protected override Task SendInformativeAsync(string text, int sequenceNumber, CancellationToken cancellationToken)
        {
            var activity = new Activity
            {
                Type = ActivityTypes.Typing,
                Text = text,
                Entities =
                [
                    new StreamInfo
                    {
                        StreamType = StreamTypes.Informative,
                        StreamSequence = sequenceNumber,
                    }
                ]
            };

            return SendStreamActivityAsync(activity, cancellationToken);
        }

        protected override async Task FinalizeStreamAsync(bool streamedPath, CancellationToken cancellationToken)
        {
            if (!IsStreamingChannel)
            {
                if (UpdatesSent() > 0 || FinalMessage != null || !string.IsNullOrWhiteSpace(Message))
                {
                    if (_streamTimedOut)
                    {
                        await UpdateActivityAsync(CreateFinalMessage(), cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await _context.SendActivityAsync(CreateFinalMessage(), cancellationToken).ConfigureAwait(false);
                    }
                }

                return;
            }

            if (UpdatesSent() > 0 || FinalMessage != null)
            {
                try
                {
                    await SendStreamActivityAsync(CreateFinalMessage(), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _context.Adapter?.Logger?.LogWarning(
                        "Exception during final StreamingResponse message: {ExceptionMessage}",
                        ex.Message);
                    System.Diagnostics.Trace.WriteLine($"Exception during final StreamingResponse message: {ex.Message}");
                }
            }
        }

        protected override async Task<StreamErrorAction> HandleSendErrorAsync(Exception exception, CancellationToken cancellationToken)
        {
            if (exception is ErrorResponseException errorResponse)
            {
                if (TeamsStreamCancelled.Equals(errorResponse.Body?.Error?.Code, StringComparison.OrdinalIgnoreCase))
                {
                    if (TeamsStreamTimedOut.Equals(errorResponse.Body?.Error?.Message, StringComparison.OrdinalIgnoreCase))
                    {
                        _context.Adapter?.Logger?.LogWarning(
                            "Client canceled due to exceeded allowed streaming time. {ErrorCode} - {ErrorMessage}",
                            errorResponse.Body?.Error?.Code,
                            errorResponse.Body?.Error?.Message);

                        _streamTimedOut = true;
                        await UpdateActivityAsync(CreateStreamTimedOutMessage(), cancellationToken).ConfigureAwait(false);
                        return StreamErrorAction.FallbackToNonStreaming;
                    }

                    _context.Adapter?.Logger?.LogWarning("User canceled stream on the client side.");
                    System.Diagnostics.Trace.WriteLine("User canceled stream on the client side.");
                    UserCancelledStream = true;
                    return StreamErrorAction.Cancel;
                }

#pragma warning disable CA1862 // Support target frameworks without the StringComparison overload.
                if (BadArgument.Equals(errorResponse.Body?.Error?.Code, StringComparison.OrdinalIgnoreCase)
                    && errorResponse.Body?.Error?.Message?.ToLower().Contains(TeamsStreamNotAllowed) == true)
                {
                    _context.Adapter?.Logger?.LogWarning(
                        "Interaction Context does not support StreamingResponse, StreamingResponse has been disabled for this turn");
                    System.Diagnostics.Trace.WriteLine(
                        "Interaction Context does not support StreamingResponse, StreamingResponse has been disabled for this turn");
                    return StreamErrorAction.FallbackToNonStreaming;
                }
#pragma warning restore CA1862

                var errorMessage = errorResponse.Body?.Error?.Message ?? "None";
                _context.Adapter?.Logger?.LogWarning(
                    "Exception during StreamingResponse: {ExceptionMessage} - {ErrorMessage}",
                    exception.Message,
                    errorMessage);
                System.Diagnostics.Trace.WriteLine(
                    $"Exception during StreamingResponse: {exception.Message} - {errorMessage}");
            }

            return StreamErrorAction.Cancel;
        }

        public override async Task<bool> SendStreamTimedOutNotification(
            string message,
            CancellationToken cancellationToken = default)
        {
            if (!IsStreamingChannel)
            {
                return false;
            }

            await SendStreamActivityAsync(CreateStreamStoppedMessage(message), cancellationToken).ConfigureAwait(false);
            FallbackToNonStreaming();
            return true;
        }

        protected virtual IActivity CreateFinalMessage()
        {
            var activity = FinalMessage ?? new Activity();

            activity.Type = ActivityTypes.Message;
            activity.Entities ??= [];
            if (FinalMessage == null)
            {
                activity.Text = !string.IsNullOrEmpty(Message) ? Message : "No text was streamed";
            }

            foreach (var streamInfo in activity.Entities
                .Where(entity => string.Equals(EntityTypes.StreamInfo, entity.Type, StringComparison.OrdinalIgnoreCase))
                .ToList())
            {
                activity.Entities.Remove(streamInfo);
            }

            if (IsStreamingChannel)
            {
                activity.Entities.Add(new StreamInfo
                {
                    StreamType = StreamTypes.Final,
                    StreamResult = string.IsNullOrEmpty(Message) ? StreamResults.Error : StreamResults.Success
                });
            }

            if (_streamTimedOut && !string.IsNullOrEmpty(StreamId))
            {
                activity.Id = StreamId;
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

            List<ClientCitation>? currentCitations = CitationUtils.GetUsedCitations(Message, Citations);
            if (EnableGeneratedByAILabel == true || currentCitations != null)
            {
                AIEntity entity = new()
                {
                    Citation = currentCitations,
                    UsageInfo = SensitivityLabel
                };

                if (EnableGeneratedByAILabel == true)
                {
                    entity.AdditionalType.Add(AIEntity.AdditionalTypeAIGeneratedContent);
                }

                activity.Entities.Add(entity);
            }

            if (Attachments != null && Attachments.Count > 0)
            {
                if (activity.Attachments == null)
                {
                    activity.Attachments = Attachments;
                }
                else if (!ReferenceEquals(activity.Attachments, Attachments))
                {
                    foreach (var attachment in Attachments)
                    {
                        activity.Attachments.Add(attachment);
                    }
                }
            }

            return activity;
        }

        protected Activity CreateStreamTimedOutMessage()
        {
            return new Activity
            {
                Type = ActivityTypes.Message,
                Id = StreamId,
                Text = !string.IsNullOrEmpty(Message)
                    ? $"{Message} {Environment.NewLine}{Environment.NewLine} {StreamingTakingTooLongMessage} {Environment.NewLine}"
                    : StreamingTakingTooLongMessage
            };
        }

        protected async Task SendStreamActivityAsync(IActivity activity, CancellationToken cancellationToken)
        {
            if (activity == null)
            {
                return;
            }

            var streamInfo = activity.GetStreamingEntity();
            if (!string.IsNullOrEmpty(StreamId))
            {
                activity.Id = StreamId;
                if (streamInfo != null)
                {
                    streamInfo.StreamId = StreamId;
                }
            }

            var response = await _context.SendActivityAsync(activity, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(StreamId))
            {
                StreamId = response.Id;
            }
        }

        protected async Task<ResourceResponse> UpdateActivityAsync(
            IActivity activity,
            CancellationToken cancellationToken = default)
        {
            if (activity == null)
            {
                return null;
            }

            try
            {
                return await _context.UpdateActivityAsync(activity, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (ex is ErrorResponseException errorResponse)
                {
                    _context.Adapter?.Logger?.LogWarning(
                        "Exception during StreamingResponse UpdateActivity: {ExceptionMessage} - {ErrorMessage}",
                        ex.Message,
                        errorResponse.Body?.Error?.Message ?? "None");
                }
                else
                {
                    _context.Adapter?.Logger?.LogWarning(
                        "Exception during StreamingResponse UpdateActivity: {ExceptionMessage}",
                        ex.Message);
                }

                System.Diagnostics.Trace.WriteLine(
                    $"Exception during StreamingResponse UpdateActivity: {ex.Message}");
                return null;
            }
        }

        protected override void OnReset()
        {
            _streamTimedOut = false;
        }

        private Activity CreateStreamStoppedMessage(string message)
        {
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = !string.IsNullOrEmpty(message) ? message : "No text was streamed",
                Entities = []
            };

            if (IsStreamingChannel)
            {
                activity.Entities.Add(new StreamInfo
                {
                    StreamType = StreamTypes.Final,
                    StreamResult = string.IsNullOrEmpty(Message) ? StreamResults.Error : StreamResults.Success
                });
            }

            return activity;
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
            else if (Channels.Webchat == turnContext.Activity.ChannelId?.Channel
                || Channels.Directline == turnContext.Activity.ChannelId?.Channel)
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
    }
}
