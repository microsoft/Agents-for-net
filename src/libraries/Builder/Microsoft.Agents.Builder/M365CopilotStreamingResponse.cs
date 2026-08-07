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
    internal sealed class M365CopilotStreamingResponse : StreamingResponse
    {
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
                return true;
            }

            var now = DateTime.UtcNow;
            if (now - _streamStartTime.Value >= StreamingTimeout)
            {
                var timeoutActivities = new List<IActivity>();
                if (string.IsNullOrEmpty(Message))
                {
                    timeoutActivities.Add(CreateTimeoutActivity(addStreamFinal: true));
                }
                else
                {
                    timeoutActivities.Add(CreateTimeoutActivity(addStreamFinal: false));
                    timeoutActivities.Add(CreateTimeoutActivity(addStreamFinal: true));
                }

                foreach (var activity in timeoutActivities)
                {
                    await SendStreamActivityAsync(activity, cancellationToken).ConfigureAwait(false);
                }

                FallbackToNonStreaming();
                return false;
            }

            if (isQueueEmpty
                && now - (_lastActivityTime ?? _streamStartTime.Value) > WorkingNoticeInterval)
            {
                var notice = string.IsNullOrWhiteSpace(_lastInformationalMessage)
                    ? StreamingTakingTooLongMessage
                    : _lastInformationalMessage;
                await QueueInformativeUpdateAsync(notice, cancellationToken).ConfigureAwait(false);
            }

            return true;
        }

        protected override void OnSendCompleted(bool isInformative, string text)
        {
            var now = DateTime.UtcNow;
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
        }

        private Activity CreateTimeoutActivity(bool addStreamFinal)
        {
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
