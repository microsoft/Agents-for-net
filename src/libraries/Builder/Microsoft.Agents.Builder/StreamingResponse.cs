// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Observable-driven implementation of <see cref="IStreamingResponse"/>.
    /// </summary>
    internal class StreamingResponse : IStreamingResponse
    {
        public static readonly int DefaultEndStreamTimeout = (int)TimeSpan.FromMinutes(2).TotalMilliseconds;

        private readonly TurnContext _context;
        private string _streamId = string.Empty;
        private ISubject<IActivity> _activitiesSubject;
        private ISubject<string> _textChunksSubject;
        private string _message = "";

        public StreamingResponse(TurnContext turnContext)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));

            _context = turnContext;
            _activitiesSubject = Subject.Synchronize(new Subject<IActivity>());
            _textChunksSubject = Subject.Synchronize(new Subject<string>());

            SetDefaults(turnContext);

            _textChunksSubject.Subscribe(textChunk =>
            {
                _message += textChunk;

                _message = CitationUtils.FormatCitationsResponse(_message);

                var activity = new Activity
                {
                    Type = ActivityTypes.Typing,
                    Text = _message,
                    Entities = []
                };

                var sequence = 0; // TODO: Implement proper sequence numbering.

                activity.Entities.Add(new StreamInfo()
                {
                    StreamType = StreamTypes.Streaming,
                    StreamSequence = sequence,
                });

                if (Citations != null && Citations.Count > 0)
                {
                    // If there are citations, filter out the citations unused in content.
                    List<ClientCitation>? currCitations = CitationUtils.GetUsedCitations(_message, Citations);
                    AIEntity entity = new();
                    if (currCitations != null && currCitations.Count > 0)
                    {
                        entity.Citation = currCitations;
                    }

                    activity.Entities.Add(entity);
                }

                _activitiesSubject.OnNext(activity);
            });

            _activitiesSubject.Subscribe(async activity =>
            {
                await SendActivityAsync(activity, CancellationToken.None).ConfigureAwait(false);
            });
        }

        /// <inheritdoc />
        public IActivity FinalMessage { get; set; }

        /// <inheritdoc />
        public int Interval { get; set; }

        /// <inheritdoc />
        public int EndStreamTimeout { get; set; } = DefaultEndStreamTimeout;

        /// <inheritdoc />
        public bool IsStreamingChannel { get; private set; }

        /// <inheritdoc />
        public string Message
        {
            get => Volatile.Read(ref _message);
            private set => Volatile.Write(ref _message, value);
        }

        /// <inheritdoc />
        public bool? EnableGeneratedByAILabel { get; set; } = false;

        /// <inheritdoc />
        public SensitivityUsageInfo? SensitivityLabel { get; set; }

        #region Citations
        /// <inheritdoc />
        public List<ClientCitation>? Citations { get; set; } = [];

        /// <inheritdoc />
        public void AddCitation(ClientCitation citation)
        {
            Citations ??= [];
            Citations.Add(citation);
        }

        /// <inheritdoc />
        public void AddCitation(Citation citation, int citationPosition)
        {
            Citations ??= [];
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

        /// <inheritdoc />
        public void AddCitations(IList<Citation> citations)
        {
            if (citations.Count > 0)
            {
                Citations ??= [];

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

        /// <inheritdoc />
        public void AddCitations(IList<ClientCitation> citations)
        {
            if (citations.Count > 0)
            {
                Citations ??= [];
                Citations.AddRange(citations);
            }
        }
        #endregion

        /// <inheritdoc />
        public async Task<StreamingResponseResult> EndStreamAsync(CancellationToken cancellationToken = default)
        {
            return StreamingResponseResult.Success;
        }

        /// <inheritdoc />
        public bool IsStreamStarted()
        {
            return true;
        }

        /// <inheritdoc />
        public async Task QueueInformativeUpdateAsync(string text, CancellationToken cancellationToken = default)
        {
            if (!IsStreamingChannel)
            {
                return;
            }

            var activity = new Activity
            {
                Type = ActivityTypes.Typing,
                Text = text,
                Entities = [new StreamInfo()
                {
                    StreamType = StreamTypes.Informative,
                    StreamSequence = 0, // TODO: Incremental sequence number
                }]
            };

            _activitiesSubject.OnNext(activity);
        }

        /// <inheritdoc />
        public void QueueTextChunk(string text)
        {
            _textChunksSubject.OnNext(text);
        }

        /// <inheritdoc />
        public async Task ResetAsync(CancellationToken cancellationToken = default)
        {
            var newActivitiesSubject = Subject.Synchronize(new Subject<IActivity>());
            var oldActivitiesSubject = Interlocked.Exchange(ref _activitiesSubject, newActivitiesSubject);
            oldActivitiesSubject.OnCompleted();

            var newTextChunksSubject = Subject.Synchronize(new Subject<string>());
            var oldTextChunksSubject = Interlocked.Exchange(ref _textChunksSubject, newTextChunksSubject);
            oldTextChunksSubject.OnCompleted();

            _message = "";
            FinalMessage = null;
            _streamId = null;
        }

        /// <inheritdoc />
        public int UpdatesSent()
        {
            return 0;
        }

        private void SetDefaults(TurnContext turnContext)
        {
            var isTeamsChannel = Channels.Msteams == turnContext.Activity.ChannelId?.Channel;

            if (string.Equals(DeliveryModes.ExpectReplies, turnContext.Activity.DeliveryMode, StringComparison.OrdinalIgnoreCase))
            {
                // No point in streaming for ExpectReplies.  Treat as non-streaming channel.
                IsStreamingChannel = false;
            }
            else if (isTeamsChannel)
            {
                if (turnContext.Activity.IsAgenticRequest())
                {
                    // Agentic requests do not support streaming responses at this time.
                    // TODO : Enable streaming for agentic requests when supported.
                    IsStreamingChannel = false;
                }
                else
                {
                    // Teams MUST use the Activity.Id returned from the first Informative message for
                    // subsequent intermediate messages.  Do not set StreamId here.

                    Interval = 1000;
                    IsStreamingChannel = true;
                }
            }
            else if (Channels.Webchat == turnContext.Activity.ChannelId?.Channel || Channels.Directline == turnContext.Activity.ChannelId?.Channel)
            {
                Interval = 500;
                IsStreamingChannel = true;

                // WebChat will use whatever StreamId is created.
                _streamId = Guid.NewGuid().ToString();
            }
            else if (string.Equals(DeliveryModes.Stream, turnContext.Activity.DeliveryMode, StringComparison.OrdinalIgnoreCase))
            {
                // Support streaming for DeliveryMode.Stream
                IsStreamingChannel = true;
                Interval = 100;
                _streamId = Guid.NewGuid().ToString();
            }
            else
            {
                IsStreamingChannel = false;
            }
        }

        private async Task SendActivityAsync(IActivity activity, CancellationToken cancellationToken)
        {
            if (activity == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(_streamId))
            {
                activity.Id = _streamId;
                activity.GetStreamingEntity().StreamId = _streamId;
            }

            try
            {
                var response = await _context.SendActivityAsync(activity, cancellationToken).ConfigureAwait(false);

                if (string.IsNullOrEmpty(_streamId))
                {
                    _streamId = response.Id;
                }
            }
            catch (Exception ex)
            {
                if (ex is ErrorResponseException errorResponse)
                {
                    _context?.Adapter?.Logger?.LogWarning(errorResponse, "Error sending streaming activity.");
                }
            }
        }

    }
}
