// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Applies the M365 Copilot (BizChat) keep-alive and stream-duration requirements.
    /// </summary>
    /// <remarks>
    /// BizChat requires periodic activity to keep the client-side stream alive and also imposes a
    /// maximum streaming window. When that window expires, this response closes the active stream
    /// and switches to normal message delivery so generation can continue without losing the result.
    /// </remarks>
    internal sealed class M365CopilotStreamingResponse : TeamsStreamingResponse
    {
        // Leave margin below the client limits so network and service latency do not cause the
        // client to expire the stream before our keep-alive or termination activity arrives.
        internal static readonly TimeSpan DefaultStreamingTimeout = TimeSpan.FromSeconds(105);
        internal static readonly TimeSpan DefaultWorkingNoticeInterval = TimeSpan.FromSeconds(35);

        private DateTime? _streamStartTime;
        private DateTime? _lastActivityTime;
        private string _lastInformationalMessage = string.Empty;

        internal TimeSpan StreamingTimeout { get; set; } = DefaultStreamingTimeout;
        internal TimeSpan WorkingNoticeInterval { get; set; } = DefaultWorkingNoticeInterval;

        public M365CopilotStreamingResponse(TurnContext turnContext)
            : base(turnContext)
        {
        }

        protected override async Task<bool> OnBeforeSendIntervalAsync(
            bool isQueueEmpty,
            CancellationToken cancellationToken)
        {
            if (_streamStartTime == null)
            {
                // The timeout window starts after the first successful send, not when text is queued.
                return true;
            }

            var now = DateTime.UtcNow;
            if (now - _streamStartTime.Value >= StreamingTimeout)
            {
                var timeoutActivities = new List<IActivity>();
                if (string.IsNullOrEmpty(Message))
                {
                    // No answer content has reached the stream, so close it with a single final
                    // activity whose error result tells the client no partial answer is available.
                    timeoutActivities.Add(CreateTimeoutActivity(addStreamFinal: true));
                }
                else
                {
                    // BizChat needs the latest buffered text as a streaming update before the final
                    // activity closes the stream. Sending both preserves partial content in the UI.
                    timeoutActivities.Add(CreateTimeoutActivity(addStreamFinal: false));
                    timeoutActivities.Add(CreateTimeoutActivity(addStreamFinal: true));
                }

                foreach (var activity in timeoutActivities)
                {
                    await SendStreamActivityAsync(activity, cancellationToken).ConfigureAwait(false);
                }

                // End only the streaming transport. The caller may continue generating content,
                // which EndStreamAsync will later deliver as a normal, complete message.
                FallbackToNonStreaming();
                return false;
            }

            if (isQueueEmpty
                && now - (_lastActivityTime ?? _streamStartTime.Value) > WorkingNoticeInterval)
            {
                // An idle BizChat stream is treated as failed by the client. Reuse the most recent
                // informative text when possible so the keep-alive remains natural to the user.
                var notice = string.IsNullOrWhiteSpace(_lastInformationalMessage)
                    ? StreamingTakingTooLongMessage
                    : _lastInformationalMessage;
                await SendInformativeAsync(
                    notice,
                    GetNextSequenceNumber(),
                    cancellationToken).ConfigureAwait(false);
                OnSendCompleted(isInformative: true, notice);
            }

            return true;
        }

        protected override void OnSendCompleted(bool isInformative, string text)
        {
            var now = DateTime.UtcNow;
            // Measure both limits from successful sends. Queued or failed activities do not reset
            // the client-side inactivity timer.
            _streamStartTime ??= now;
            _lastActivityTime = now;

            if (isInformative)
            {
                _lastInformationalMessage = text;
            }
        }

        protected override void OnReset()
        {
            base.OnReset();
            _streamStartTime = null;
            _lastActivityTime = null;
            _lastInformationalMessage = string.Empty;
            StreamingTimeout = DefaultStreamingTimeout;
            WorkingNoticeInterval = DefaultWorkingNoticeInterval;
        }

        private Activity CreateTimeoutActivity(bool addStreamFinal)
        {
            // The non-final form advances the existing streamed response; the final form closes
            // that same stream. SendStreamActivityAsync applies the shared StreamId to both.
            var activity = new Activity
            {
                Type = addStreamFinal ? ActivityTypes.Message : ActivityTypes.Typing,
                Text = !string.IsNullOrEmpty(Message)
                    ? $"{Message} {Environment.NewLine}{Environment.NewLine} {StreamingTakingTooLongMessage} {Environment.NewLine}"
                    : StreamingTakingTooLongMessage,
                Entities = []
            };

            if (addStreamFinal)
            {
                activity.Entities.Add(new StreamInfo
                {
                    StreamType = StreamTypes.Final,
                    StreamId = StreamId,
                    StreamResult = string.IsNullOrEmpty(Message) ? StreamResults.Error : StreamResults.Success
                });
            }
            else
            {
                activity.Entities.Add(new StreamInfo
                {
                    StreamType = StreamTypes.Streaming,
                    StreamSequence = GetNextSequenceNumber()
                });
            }

            return activity;
        }
    }
}
