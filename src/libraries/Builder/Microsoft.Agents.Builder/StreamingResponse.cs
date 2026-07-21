// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.State;
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
    /// <remarks>
    /// This class support throttling via the <see cref="StreamingResponseBase.Interval"/> property.  Teams and Azure Channels require
    /// some throttling since services like OpenAI produce streams that exceed allowed Channel message limits.
    /// Teams defaults to 1000ms per intermediate message, and WebChat 500ms.  Reducing the Interval could result
    /// in message delivery failures.
    /// </remarks>
    internal class StreamingResponse : StreamingResponseBase
    {
        private const string TeamsStreamCancelled = "ContentStreamNotAllowed";
        // Teams failed to accept streaming messages. 
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
            Core.AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));

            _context = turnContext;
            SetDefaults(turnContext);
        }

        /// <summary>
        /// If there are citations, modify the content so that the sources are numbers instead of [doc1], [doc2], etc.
        /// </summary>
        protected override string TransformBufferedText(string bufferedText)
        {
            return CitationUtils.FormatCitationsResponse(bufferedText);
        }

        /// <inheritdoc/>
        protected override async Task SendChunkAsync(string bufferedText, int sequenceNumber, CancellationToken cancellationToken)
        {
            var activity = new Activity
            {
                Type = ActivityTypes.Typing,
                Text = bufferedText,
                Entities =
                [
                    new StreamInfo()
                    {
                        StreamType = StreamTypes.Streaming,
                        StreamSequence = sequenceNumber,
                    }
                ]
            };

            if (Citations != null && Citations.Count > 0)
            {
                // If there are citations, filter out the citations unused in content.
                List<ClientCitation>? currCitations = CitationUtils.GetUsedCitations(bufferedText, Citations);
                AIEntity entity = new();
                if (currCitations != null && currCitations.Count > 0)
                {
                    entity.Citation = currCitations;
                }

                activity.Entities.Add(entity);
            }

            await SendStreamActivityAsync(activity, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        protected override async Task SendInformativeAsync(string text, int sequenceNumber, CancellationToken cancellationToken)
        {
            var activity = new Activity
            {
                Type = ActivityTypes.Typing,
                Text = text,
                Entities =
                [
                    new StreamInfo()
                    {
                        StreamType = StreamTypes.Informative,
                        StreamSequence = sequenceNumber,
                    }
                ]
            };

            await SendStreamActivityAsync(activity, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        protected override async Task FinalizeStreamAsync(bool streamedPath, CancellationToken cancellationToken)
        {
            if (!streamedPath)
            {
                // Non-streaming channel: send the buffered Message as a normal message.
                if (UpdatesSent() > 0 || FinalMessage != null || !string.IsNullOrWhiteSpace(Message))
                {
                    await _context.SendActivityAsync(CreateFinalMessage(), cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                if (UpdatesSent() > 0 || FinalMessage != null)
                {
                    try
                    {
                        await SendStreamActivityAsync(CreateFinalMessage(), cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // A failed final streaming send is not fatal to the turn; the result has already been computed.
                        _context?.Adapter?.Logger?.LogWarning("Exception during final StreamingResponse message: {ExceptionMessage}", ex.Message);
                        System.Diagnostics.Trace.WriteLine($"Exception during final StreamingResponse message: {ex.Message}");
                    }
                }
            }
        }

        /// <inheritdoc/>
        protected override Task<StreamErrorAction> HandleSendErrorAsync(Exception exception, CancellationToken cancellationToken)
        {
            if (exception is ErrorResponseException errorResponse)
            {
                // User canceled?
                if (TeamsStreamCancelled.Equals(errorResponse?.Body?.Error?.Code, StringComparison.OrdinalIgnoreCase))
                {
                    _context?.Adapter?.Logger?.LogWarning("User canceled stream on the client side.");
                    System.Diagnostics.Trace.WriteLine("User canceled stream on the client side.");

                    UserCancelledStream = true;
                    return Task.FromResult(StreamErrorAction.Cancel);
                }

                // Stream not allowed?
#pragma warning disable CA1862 // Use the 'StringComparison' method overloads to perform case-insensitive string comparisons - this is to support older .NET versions
                if (BadArgument.Equals(errorResponse?.Body?.Error?.Code, StringComparison.OrdinalIgnoreCase) &&
                    errorResponse?.Body?.Error?.Message.ToLower().Contains(TeamsStreamNotAllowed) == true)
                {
                    _context?.Adapter?.Logger?.LogWarning("Interaction Context does not support StreamingResponse, StreamingResponse has been disabled for this turn");
                    System.Diagnostics.Trace.WriteLine("Interaction Context does not support StreamingResponse, StreamingResponse has been disabled for this turn");

                    // Disable Streaming for this channel / interaction as Teams will not accept it at this time.
                    return Task.FromResult(StreamErrorAction.FallbackToNonStreaming);
                }
#pragma warning restore CA1862 // Use the 'StringComparison' method overloads to perform case-insensitive string comparisons

                var errorMessage = errorResponse?.Body?.Error?.Message ?? "None";

                _context?.Adapter?.Logger?.LogWarning(
                    "Exception during StreamingResponse: {ExceptionMessage} - {ErrorMessage}",
                    exception.Message,
                    errorMessage);

                System.Diagnostics.Trace.WriteLine($"Exception during StreamingResponse: {exception.Message} - {errorMessage}");
            }

            return Task.FromResult(StreamErrorAction.Cancel);
        }

        private IActivity CreateFinalMessage()
        {
            var activity = FinalMessage ?? new Activity();

            activity.Type = ActivityTypes.Message;
            activity.Entities ??= [];
            if (FinalMessage == null)
            {
                activity.Text = !string.IsNullOrEmpty(Message) ? Message : "No text was streamed";   // Teams won't allow Activity.Text changes or empty text
            }

            // make sure the supplied Activity doesn't have a streamInfo already.
            var existingStreamInfos = activity.Entities.Where(e => string.Equals(EntityTypes.StreamInfo, e.Type, StringComparison.OrdinalIgnoreCase)).ToList();
            if (existingStreamInfos.Count != 0)
            {
                foreach (var existing in existingStreamInfos)
                {
                    activity.Entities.Remove(existing);
                }
            }

            if (IsStreamingChannel)
            {
                // Only append this if the channel supports streaming.
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

            // Add in Generated by AI
            List<ClientCitation>? currCitations = CitationUtils.GetUsedCitations(Message, Citations);
            if ((bool)EnableGeneratedByAILabel || currCitations != null)
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

            // Add Attachments if there are any
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

        private void SetDefaults(TurnContext turnContext)
        {
            _isTeamsChannel = Channels.Msteams == turnContext.Activity.ChannelId?.Channel;

            if (string.Equals(DeliveryModes.ExpectReplies, turnContext.Activity.DeliveryMode, StringComparison.OrdinalIgnoreCase))
            {
                // No point in streaming for ExpectReplies.  Treat as non-streaming channel.
                IsStreamingChannel = false;
            }
            else if (_isTeamsChannel)
            {
                // Teams MUST use the Activity.Id returned from the first Informative message for
                // subsequent intermediate messages.  Do not set StreamId here.

                Interval = 1000;
                IsStreamingChannel = true;
            }
            else if (Channels.Webchat == turnContext.Activity.ChannelId?.Channel || Channels.Directline == turnContext.Activity.ChannelId?.Channel)
            {
                Interval = 500;
                IsStreamingChannel = true;

                // WebChat will use whatever StreamId is created.
                StreamId = Guid.NewGuid().ToString();
            }
            else if (string.Equals(DeliveryModes.Stream, turnContext.Activity.DeliveryMode, StringComparison.OrdinalIgnoreCase))
            {
                // Support streaming for DeliveryMode.Stream
                IsStreamingChannel = true;
                Interval = 100;
                StreamId = Guid.NewGuid().ToString();
            }
            else
            {
                IsStreamingChannel = false;
            }
        }

        /// <summary>
        /// Send an intermediate/final streaming activity, applying (or capturing) the StreamId.
        /// Exceptions are allowed to propagate so the base can dispatch them to <see cref="HandleSendErrorAsync"/>.
        /// </summary>
        private async Task SendStreamActivityAsync(IActivity activity, CancellationToken cancellationToken)
        {
            if (activity == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(StreamId))
            {
                activity.Id = StreamId;
                activity.GetStreamingEntity().StreamId = StreamId;
            }

            var response = await _context.SendActivityAsync(activity, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(StreamId))
            {
                StreamId = response.Id;
            }
        }
    }
}
