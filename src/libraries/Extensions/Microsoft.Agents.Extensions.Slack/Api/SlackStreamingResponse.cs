// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System;
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

        public SlackStreamingResponse(ITurnContext turnContext, SlackApi slackApi)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
            AssertionHelpers.ThrowIfNull(slackApi, nameof(slackApi));

            _turnContext = turnContext;
            _slackApi = slackApi;

            IsStreamingChannel = true;
            Interval = 200;
        }

        /// <inheritdoc/>
        protected override async Task SendChunkAsync(string bufferedText, int sequenceNumber, CancellationToken cancellationToken)
        {
            await EnsureStreamAsync().ConfigureAwait(false);
            StreamId ??= Guid.NewGuid().ToString();

            // Slack appends, so only send the portion of the buffered text not yet streamed.
            bufferedText ??= "";
            var start = Math.Min(_sentLength, bufferedText.Length);
            var delta = bufferedText.Substring(start);
            _sentLength = bufferedText.Length;

            if (string.IsNullOrEmpty(delta))
            {
                return;
            }

            await _stream.AppendAsync(new MarkdownTextChunk(delta)).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        protected override async Task SendInformativeAsync(string text, int sequenceNumber, CancellationToken cancellationToken)
        {
            await EnsureStreamAsync().ConfigureAwait(false);
            StreamId ??= Guid.NewGuid().ToString();
            await _stream.AppendAsync(new TaskUpdateChunk(StreamId, text ?? "", SlackTaskStatus.InProgress)).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        protected override async Task FinalizeStreamAsync(bool streamedPath, CancellationToken cancellationToken)
        {
            // The base drains buffered chunks before calling this, so the stream already contains the full text.
            if (_stream != null)
            {
                await _stream.AppendAsync(new TaskUpdateChunk(id: StreamId, title: "Done", status: SlackTaskStatus.Complete));

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
                
                await _stream.StopAsync(blocks: feedbackButtons).ConfigureAwait(false);
            }

            StreamId = null;
        }

        /// <inheritdoc/>
        protected override Task<StreamErrorAction> HandleSendErrorAsync(Exception exception, CancellationToken cancellationToken)
        {
            System.Diagnostics.Trace.WriteLine($"Exception during Slack StreamingResponse: {exception.Message}");
            return Task.FromResult(StreamErrorAction.Cancel);
        }

        /// <inheritdoc/>
        protected override void OnReset()
        {
            _stream = null;
            _sentLength = 0;
        }

        private async Task EnsureStreamAsync()
        {
            if (_stream != null)
            {
                return;
            }

            var channelData = _turnContext.Activity.GetChannelData<SlackChannelData>()
                ?? throw new InvalidOperationException("Slack streaming requires SlackChannelData on the incoming activity.");

            var stream = new SlackStream(_slackApi, channelData.Channel, channelData.ThreadTs, channelData.ApiToken);
            _stream = await stream.StartAsync().ConfigureAwait(false);
        }
    }
}
