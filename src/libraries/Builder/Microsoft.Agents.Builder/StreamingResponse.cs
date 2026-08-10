// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

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
    /// Streams Activity Protocol responses for WebChat, DirectLine, and DeliveryMode.Stream.
    /// </summary>
    internal class StreamingResponse : StreamingResponseBase
    {
        public StreamingResponse(TurnContext turnContext)
        {
            Core.AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));

            Context = turnContext;
            SetDefaults(turnContext);
        }

        protected TurnContext Context { get; }

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
                    await SendNonStreamingFinalMessageAsync(
                        CreateFinalMessage(),
                        cancellationToken).ConfigureAwait(false);
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
                    Context.Adapter?.Logger?.LogWarning(
                        "Exception during final StreamingResponse message: {ExceptionMessage}",
                        ex.Message);
                    System.Diagnostics.Trace.WriteLine($"Exception during final StreamingResponse message: {ex.Message}");
                }
            }
        }

        protected override Task<StreamErrorAction> HandleSendErrorAsync(
            Exception exception,
            CancellationToken cancellationToken)
        {
            Context.Adapter?.Logger?.LogWarning(
                "Exception during StreamingResponse: {ExceptionMessage}",
                exception.Message);
            System.Diagnostics.Trace.WriteLine(
                $"Exception during StreamingResponse: {exception.Message}");
            return Task.FromResult(StreamErrorAction.Cancel);
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

            var response = await Context.SendActivityAsync(activity, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(StreamId))
            {
                StreamId = response.Id;
            }
        }

        protected virtual Task<ResourceResponse> SendNonStreamingFinalMessageAsync(
            IActivity activity,
            CancellationToken cancellationToken = default)
        {
            return Context.SendActivityAsync(activity, cancellationToken);
        }

        protected override void OnReset()
        {
            SetDefaults(Context);
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
            Interval = 0;
            IsStreamingChannel = false;
            StreamId = null;

            if (string.Equals(DeliveryModes.ExpectReplies, turnContext.Activity.DeliveryMode, StringComparison.OrdinalIgnoreCase))
            {
                return;
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
