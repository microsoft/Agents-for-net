// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage.Transcript;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    internal class Transcript
    {
        private static readonly TraceTranscriptLogger _trace = new();
        private static readonly ConcurrentQueue<IActivity> _transcript = new();
        private readonly ITranscriptStore _transcriptStore;

        public Transcript(AgentApplication app, ITranscriptStore transcriptStore)
        {
            _transcriptStore = transcriptStore ?? throw new ArgumentNullException(nameof(transcriptStore));
            
            app.OnBeforeTurn(OnBeforeTurn);
            app.OnAfterTurn(OnAfterTurn);
        }

        public static async Task<bool> OnBeforeTurn(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            static void LogActivity(ConcurrentQueue<IActivity> transcript, IActivity activity)
            {
                activity.Timestamp ??= DateTime.UtcNow;
                transcript.Enqueue(activity);
            }

            // log incoming activity at beginning of turn
            if (turnContext.Activity != null)
            {
                turnContext.Activity.From ??= new ChannelAccount();

                if (string.IsNullOrEmpty(turnContext.Activity.From.Role))
                {
                    turnContext.Activity.From.Role = RoleTypes.User;
                }

                // We should not log ContinueConversation events used by Agents to initialize the middleware.
                if (!(turnContext.Activity.Type == ActivityTypes.Event && turnContext.Activity.Name == ActivityEventNames.ContinueConversation))
                {
                    LogActivity(_transcript, turnContext.Activity.Clone());
                    await _trace.LogActivityAsync(turnContext.Activity);
                }
            }

            // hook up onSend pipeline
            turnContext.OnSendActivities(async (ctx, activities, nextSend) =>
            {
                // run full pipeline
                var responses = await nextSend().ConfigureAwait(false);

                foreach (var activity in activities)
                {
                    LogActivity(_transcript, activity.Clone());
                    await _trace.LogActivityAsync(activity);
                }

                return responses;
            });

            // hook up update activity pipeline
            turnContext.OnUpdateActivity(async (ctx, activity, nextUpdate) =>
            {
                // run full pipeline
                var response = await nextUpdate().ConfigureAwait(false);

                // add Message Update activity
                var updateActivity = activity.Clone();
                updateActivity.Type = ActivityTypes.MessageUpdate;
                LogActivity(_transcript, updateActivity);
                await _trace.LogActivityAsync(updateActivity);
                return response;
            });

            // hook up delete activity pipeline
            turnContext.OnDeleteActivity(async (ctx, reference, nextDelete) =>
            {
                await nextDelete().ConfigureAwait(false);

                // add MessageDelete activity
                // log as MessageDelete activity
                var deleteActivity = new Activity
                {
                    Type = ActivityTypes.MessageDelete,
                    Id = reference.ActivityId,
                }
                    .ApplyConversationReference(reference, isIncoming: false);

                LogActivity(_transcript, deleteActivity);
                await _trace.LogActivityAsync(deleteActivity);
            });

            return true;
        }

        public async Task<bool> OnAfterTurn(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            try
            {
                while (!_transcript.IsEmpty)
                {
                    if (_transcript.TryDequeue(out var activity))
                    {
                        // Process the queue and log all the activities in parallel.
                        await _transcriptStore.LogActivityAsync(activity).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"Transcript logActivity failed with {ex}");
            }

            return true;
        }
    }
}
