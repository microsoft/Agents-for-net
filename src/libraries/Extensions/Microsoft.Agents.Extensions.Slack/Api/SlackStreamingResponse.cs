// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Slack.Api
{
    /// <summary>
    /// An <see cref="IStreamingResponse"/> implementation that streams to Slack using
    /// <see cref="SlackStream"/> (chat.startStream / chat.appendStream / chat.stopStream).
    /// </summary>
    /// <remarks>
    /// Text chunks are appended to Slack as <see cref="MarkdownTextChunk"/> deltas (Slack appends, so only the
    /// newly-buffered text is sent each time), and informative updates are sent as an in-progress
    /// <see cref="TaskUpdateChunk"/>.  The Slack stream is created lazily on the first send and closed on
    /// <see cref="StreamingResponseBase.EndStreamAsync(CancellationToken)"/>.
    /// </remarks>
    internal class SlackStreamingResponse : StreamingResponseBase
    {
        private readonly ITurnContext _turnContext;
        private readonly SlackApi _slackApi;

        private SlackStream _stream;
        private int _sentLength;
        private int _defaultInterval = 200;
        private int _defaultInitialDelay = 250;

        public SlackStreamingResponse(ITurnContext turnContext, SlackApi slackApi)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
            AssertionHelpers.ThrowIfNull(slackApi, nameof(slackApi));

            _turnContext = turnContext;
            _slackApi = slackApi;

            IsStreamingChannel = true;
            Interval = 200;
        }

        internal void ConfigureDefaults(int interval, int initialDelay)
        {
            Interval = interval;
            InitialDelay = initialDelay;
            _defaultInterval = interval;
            _defaultInitialDelay = initialDelay;
        }

        /// <inheritdoc/>
        protected override async Task SendChunkAsync(string bufferedText, int sequenceNumber, CancellationToken cancellationToken)
        {
            await EnsureStreamAsync(cancellationToken).ConfigureAwait(false);
            StreamId ??= Guid.NewGuid().ToString();

            // Slack appends, so only send the portion of the buffered text not yet streamed.
            bufferedText ??= "";
            var start = Math.Min(_sentLength, bufferedText.Length);
            var delta = bufferedText.Substring(start);

            if (string.IsNullOrEmpty(delta))
            {
                return;
            }

            await _stream.AppendAsync([new MarkdownTextChunk(delta)], cancellationToken).ConfigureAwait(false);
            _sentLength = bufferedText.Length;
        }

        /// <inheritdoc/>
        protected override async Task SendInformativeAsync(string text, int sequenceNumber, CancellationToken cancellationToken)
        {
            await EnsureStreamAsync(cancellationToken).ConfigureAwait(false);
            StreamId ??= Guid.NewGuid().ToString();
            await _stream.AppendAsync(
                [new TaskUpdateChunk(StreamId, text ?? "", SlackTaskStatus.InProgress)],
                cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        protected override async Task FinalizeStreamAsync(bool streamedPath, CancellationToken cancellationToken)
        {
            if (_stream == null && !string.IsNullOrEmpty(Message))
            {
                await EnsureStreamAsync(cancellationToken).ConfigureAwait(false);
            }

            if (_stream == null)
            {
                StreamId = null;
                return;
            }

            StreamId ??= Guid.NewGuid().ToString();
            var chunks = new List<Chunk>();
            var delta = Message.Substring(Math.Min(_sentLength, Message.Length));
            if (!string.IsNullOrEmpty(delta))
            {
                chunks.Add(new MarkdownTextChunk(delta));
            }
            chunks.Add(new TaskUpdateChunk(id: StreamId, title: "Done", status: SlackTaskStatus.Complete));

            string feedbackButtons = null;
            if (FeedbackLoopEnabled)
            {
                feedbackButtons = """
                    {
                        "blocks": 
                        [
                            {
                                "type": "context_actions",
                                "elements": [
                                    {
                                        "type": "feedback_buttons",
                                        "action_id": "feedback",
                                        "positive_button": {
                                            "text": {
                                                "type": "plain_text",
                                                "text": "👍"
                                            },
                                            "value": "positive_feedback"
                                        },
                                        "negative_button": {
                                            "text": {
                                                "type": "plain_text",
                                                "text": "👎"
                                            },
                                            "value": "negative_feedback"
                                        }
                                    }
                                ]
                            }
                        ]
                    }
                    """;
            }

            await StopRemoteStreamAsync(feedbackButtons, chunks, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        protected override async Task<StreamErrorAction> HandleSendErrorAsync(Exception exception, CancellationToken cancellationToken)
        {
            System.Diagnostics.Trace.WriteLine($"Exception during Slack StreamingResponse: {exception.Message}");

            try
            {
                await StopRemoteStreamAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (Exception stopException)
            {
                System.Diagnostics.Trace.WriteLine(
                    $"Exception stopping Slack StreamingResponse after send failed: {stopException.Message}");
            }

            return StreamErrorAction.Cancel;
        }

        /// <inheritdoc/>
        protected override void OnReset()
        {
            _stream = null;
            _sentLength = 0;
            Interval = _defaultInterval;
            InitialDelay = _defaultInitialDelay;
            IsStreamingChannel = true;
        }

        private async Task StopRemoteStreamAsync(
            string blocks = null,
            IList<Chunk> chunks = null,
            CancellationToken cancellationToken = default)
        {
            var stream = _stream;
            if (stream == null)
            {
                StreamId = null;
                return;
            }

            try
            {
                await stream.StopAsync(chunks, blocks, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _stream = null;
                StreamId = null;
            }
        }

        private async Task EnsureStreamAsync(CancellationToken cancellationToken)
        {
            if (_stream != null)
            {
                return;
            }

            var channelData = _turnContext.Activity.GetChannelData<SlackChannelData>()
                ?? throw new InvalidOperationException("Slack streaming requires SlackChannelData on the incoming activity.");

            var stream = new SlackStream(_slackApi, channelData.Channel, channelData.ThreadTs, channelData.ApiToken);
            _stream = await stream.StartAsync(TaskDisplayMode.Plan, cancellationToken).ConfigureAwait(false);
        }
    }
}
