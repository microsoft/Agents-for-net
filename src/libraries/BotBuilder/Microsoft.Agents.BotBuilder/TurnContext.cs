﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.BotBuilder
{
    /// <summary>
    /// Provides context for a turn of a bot.
    /// </summary>
    /// <remarks>
    /// Context provides information needed to process an incoming activity.
    /// The context object is created by a <see cref="IChannelAdapter"/> and persists for the
    /// length of the turn.  TurnContext cannot be used after the turn is complete.
    /// </remarks>
    /// <seealso cref="IBot"/>
    public class TurnContext : ITurnContext, IDisposable
    {
        private readonly IList<SendActivitiesHandler> _onSendActivities = new List<SendActivitiesHandler>();
        private readonly IList<UpdateActivityHandler> _onUpdateActivity = new List<UpdateActivityHandler>();
        private readonly IList<DeleteActivityHandler> _onDeleteActivity = new List<DeleteActivityHandler>();

        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="TurnContext"/> class.
        /// </summary>
        /// <param name="adapter">The adapter creating the context.</param>
        /// <param name="activity">The incoming activity for the turn;
        /// or <c>null</c> for a turn for a proactive message.</param>
        /// <exception cref="ArgumentNullException"><paramref name="activity"/> or
        /// <paramref name="adapter"/> is <c>null</c>.</exception>
        /// <remarks>For use by bot adapter implementations only.</remarks>
        public TurnContext(IChannelAdapter adapter, IActivity activity)
        {
            Adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            Activity = activity ?? throw new ArgumentNullException(nameof(activity));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ITurnContext"/> class from another TurnContext class to target an alternate Activity.
        /// </summary>
        /// <remarks>
        /// For supporting calling legacy systems that always assume ITurnContext.Activity is the activity should be processed.
        /// This class clones the TurnContext and then replaces the original.activity with the passed in activity.
        /// </remarks>
        /// <param name="turnContext">context to clone.</param>
        /// <param name="activity">activity to put into the new turn context.</param>
        public TurnContext(ITurnContext turnContext, IActivity activity)
        {
            ArgumentNullException.ThrowIfNull(turnContext);

            Activity = activity ?? throw new ArgumentNullException(nameof(activity));

            // all properties should be copied over except for activity.
            Adapter = turnContext.Adapter;
            TurnState = turnContext.TurnState;
            Responded = turnContext.Responded;

            if (turnContext is TurnContext tc)
            {
                BufferedReplyActivities = tc.BufferedReplyActivities;

                // keep private middleware pipeline hooks.
                _onSendActivities = tc._onSendActivities;
                _onUpdateActivity = tc._onUpdateActivity;
                _onDeleteActivity = tc._onDeleteActivity;
            }
        }

        /// <summary>
        /// Gets the bot adapter that created this context object.
        /// </summary>
        /// <value>The bot adapter that created this context object.</value>
        public IChannelAdapter Adapter { get; }

        /// <summary>
        /// Gets the services registered on this context object.
        /// </summary>
        /// <value>The services registered on this context object.</value>
        public TurnContextStateCollection TurnState { get; } = [];

        /// <summary>
        /// Gets the activity associated with this turn; or <c>null</c> when processing
        /// a proactive message.
        /// </summary>
        /// <value>The activity associated with this turn.</value>
        public IActivity Activity { get; }

        /// <summary>
        /// Gets a value indicating whether at least one response was sent for the current turn.
        /// </summary>
        /// <value><c>true</c> if at least one response was sent for the current turn.</value>
        public bool Responded
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets a list of activities to send when `context.Activity.DeliveryMode == 'expectReplies'.
        /// </summary>
        /// <value>A list of activities.</value>
        public List<IActivity> BufferedReplyActivities { get; } = new List<IActivity>();

        /// <summary>
        /// Adds a response handler for send activity operations.
        /// </summary>
        /// <param name="handler">The handler to add to the context object.</param>
        /// <returns>The updated context object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <c>null</c>.</exception>
        /// <remarks>When the context's <see cref="SendActivityAsync(IActivity, CancellationToken)"/>
        /// or <see cref="SendActivitiesAsync(IActivity[], CancellationToken)"/> methods are called,
        /// the adapter calls the registered handlers in the order in which they were
        /// added to the context object.
        /// </remarks>
        public ITurnContext OnSendActivities(SendActivitiesHandler handler)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(OnSendActivities));
            ArgumentNullException.ThrowIfNull(handler);

            _onSendActivities.Add(handler);
            return this;
        }

        /// <summary>
        /// Adds a response handler for update activity operations.
        /// </summary>
        /// <param name="handler">The handler to add to the context object.</param>
        /// <returns>The updated context object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <c>null</c>.</exception>
        /// <remarks>When the context's <see cref="UpdateActivityAsync(IActivity, CancellationToken)"/> is called,
        /// the adapter calls the registered handlers in the order in which they were
        /// added to the context object.
        /// </remarks>
        public ITurnContext OnUpdateActivity(UpdateActivityHandler handler)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(OnUpdateActivity));
            ArgumentNullException.ThrowIfNull(handler);

            _onUpdateActivity.Add(handler);
            return this;
        }

        /// <summary>
        /// Adds a response handler for delete activity operations.
        /// </summary>
        /// <param name="handler">The handler to add to the context object.</param>
        /// <returns>The updated context object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <c>null</c>.</exception>
        /// <remarks>When the context's <see cref="DeleteActivityAsync(ConversationReference, CancellationToken)"/>
        /// or <see cref="DeleteActivityAsync(string, CancellationToken)"/> is called,
        /// the adapter calls the registered handlers in the order in which they were
        /// added to the context object.
        /// </remarks>
        public ITurnContext OnDeleteActivity(DeleteActivityHandler handler)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(OnDeleteActivity));
            ArgumentNullException.ThrowIfNull(handler);

            _onDeleteActivity.Add(handler);
            return this;
        }

        /// <inheritdoc/>
        public async Task<ResourceResponse> SendActivityAsync(string textReplyToSend, string speak = null, string inputHint = null, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(SendActivityAsync));
            ArgumentException.ThrowIfNullOrWhiteSpace(textReplyToSend);

            var activityToSend = new Activity() 
            { 
                Type = ActivityTypes.Message,
                Text = textReplyToSend 
            };

            if (!string.IsNullOrEmpty(speak))
            {
                activityToSend.Speak = speak;
            }

            if (!string.IsNullOrEmpty(inputHint))
            {
                activityToSend.InputHint = inputHint;
            }

            return await SendActivityAsync(activityToSend, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<ResourceResponse> SendActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(SendActivityAsync));
            ArgumentNullException.ThrowIfNull(activity);

            ResourceResponse[] responses = await SendActivitiesAsync(new[] { activity }, cancellationToken).ConfigureAwait(false);
            if (responses == null || responses.Length == 0)
            {
                // It's possible an interceptor prevented the activity from having been sent.
                // Just return an empty response in that case.
                return new ResourceResponse();
            }
            else
            {
                return responses[0];
            }
        }

        /// <inheritdoc/>
        public Task<ResourceResponse[]> SendActivitiesAsync(IActivity[] activities, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(SendActivitiesAsync));
            ArgumentNullException.ThrowIfNull(activities);

            if (activities.Length == 0)
            {
                throw new ArgumentException("Expecting one or more activities, but the array was empty.", nameof(activities));
            }

            var conversationReference = this.Activity.GetConversationReference();

            var bufferedActivities = new List<IActivity>(activities.Length);

            for (var index = 0; index < activities.Length; index++)
            {
                // Buffer the incoming activities into a List<T> since we allow the set to be manipulated by the callbacks
                // Bind the relevant Conversation Reference properties, such as URLs and
                // ChannelId's, to the activity we're about to send
                bufferedActivities.Add(activities[index].ApplyConversationReference(conversationReference));
            }

            // If there are no callbacks registered, bypass the overhead of invoking them and send directly to the adapter
            if (_onSendActivities.Count == 0)
            {
                return SendActivitiesThroughAdapter();
            }

            // Send through the full callback pipeline
            return SendActivitiesThroughCallbackPipeline();

            Task<ResourceResponse[]> SendActivitiesThroughCallbackPipeline(int nextCallbackIndex = 0)
            {
                // If we've executed the last callback, we now send straight to the adapter
                if (nextCallbackIndex == _onSendActivities.Count)
                {
                    return SendActivitiesThroughAdapter();
                }

                return _onSendActivities[nextCallbackIndex].Invoke(this, bufferedActivities, () => SendActivitiesThroughCallbackPipeline(nextCallbackIndex + 1));
            }

            async Task<ResourceResponse[]> SendActivitiesThroughAdapter()
            {
                if (Activity.DeliveryMode == DeliveryModes.ExpectReplies)
                {
                    var responses = new ResourceResponse[bufferedActivities.Count];
                    var sentNonTraceActivity = false;

                    for (var index = 0; index < responses.Length; index++)
                    {
                        var activity = bufferedActivities[index];
                        BufferedReplyActivities.Add(activity);

                        // Ensure the TurnState has the InvokeResponseKey, since this activity
                        // is not being sent through the adapter, where it would be added to TurnState.
                        if (activity.Type == ActivityTypes.InvokeResponse)
                        {
                            TurnState.Add(ChannelAdapter.InvokeResponseKey, activity);
                        }

                        responses[index] = new ResourceResponse();

                        sentNonTraceActivity |= activity.Type != ActivityTypes.Trace;
                    }

                    if (sentNonTraceActivity)
                    {
                        Responded = true;
                    }

                    return responses;
                }
                else
                {
                    // Send from the list which may have been manipulated via the event handlers.
                    // Note that 'responses' was captured from the root of the call, and will be
                    // returned to the original caller.
                    var responses = await Adapter.SendActivitiesAsync(this, bufferedActivities.ToArray(), cancellationToken).ConfigureAwait(false);
                    var sentNonTraceActivity = false;

                    for (var index = 0; index < responses.Length; index++)
                    {
                        var activity = bufferedActivities[index];

                        activity.Id = responses[index].Id;

                        sentNonTraceActivity |= activity.Type != ActivityTypes.Trace;
                    }

                    if (sentNonTraceActivity)
                    {
                        Responded = true;
                    }

                    return responses;
                }
            }
        }

        /// <inheritdoc/>
        public async Task<ResourceResponse> UpdateActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(UpdateActivityAsync));
            ArgumentNullException.ThrowIfNull(activity);

            var conversationReference = Activity.GetConversationReference();
            var a = activity.ApplyConversationReference(conversationReference);

            async Task<ResourceResponse> ActuallyUpdateStuffAsync()
            {
                return await Adapter.UpdateActivityAsync(this, a, cancellationToken).ConfigureAwait(false);
            }

            return await UpdateActivityInternalAsync(a, _onUpdateActivity, ActuallyUpdateStuffAsync, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task DeleteActivityAsync(string activityId, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(DeleteActivityAsync));
            ArgumentException.ThrowIfNullOrWhiteSpace(activityId);

            var cr = Activity.GetConversationReference();
            cr.ActivityId = activityId;

            async Task ActuallyDeleteStuffAsync()
            {
                await Adapter.DeleteActivityAsync(this, cr, cancellationToken).ConfigureAwait(false);
            }

            await DeleteActivityInternalAsync(cr, _onDeleteActivity, ActuallyDeleteStuffAsync, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task DeleteActivityAsync(ConversationReference conversationReference, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(DeleteActivityAsync));
            ArgumentNullException.ThrowIfNull(conversationReference);

            async Task ActuallyDeleteStuffAsync()
            {
                await Adapter.DeleteActivityAsync(this, conversationReference, cancellationToken).ConfigureAwait(false);
            }

            await DeleteActivityInternalAsync(conversationReference, _onDeleteActivity, ActuallyDeleteStuffAsync, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<ResourceResponse> TraceActivityAsync(string name, object value = null, string valueType = null, [CallerMemberName] string label = null, CancellationToken cancellationToken = default)
        {
            return await SendActivityAsync(MessageFactory.CreateTrace(this.Activity, name, value, valueType, label), cancellationToken);
        }

        /// <summary>
        /// Frees resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <param name="disposing">Boolean value that determines whether to free resources or not.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                TurnState.Dispose();
            }

            _disposed = true;
        }

        private async Task<ResourceResponse> UpdateActivityInternalAsync(
            IActivity activity,
            IEnumerable<UpdateActivityHandler> updateHandlers,
            Func<Task<ResourceResponse>> callAtBottom,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(activity);

            if (updateHandlers == null)
            {
                throw new ArgumentException($"{nameof(updateHandlers)} is null.", nameof(updateHandlers));
            }

            // No middleware to run.
            if (!updateHandlers.Any())
            {
                if (callAtBottom != null)
                {
                    return await callAtBottom().ConfigureAwait(false);
                }

                return null;
            }

            // Default to "No more Middleware after this".
            async Task<ResourceResponse> NextAsync()
            {
                // Remove the first item from the list of middleware to call,
                // so that the next call just has the remaining items to worry about.
                IEnumerable<UpdateActivityHandler> remaining = updateHandlers.Skip(1);
                var result = await UpdateActivityInternalAsync(activity, remaining, callAtBottom, cancellationToken).ConfigureAwait(false);
                activity.Id = result.Id;
                return result;
            }

            // Grab the current middleware, which is the 1st element in the array, and execute it
            UpdateActivityHandler toCall = updateHandlers.First();
            return await toCall(this, activity, NextAsync).ConfigureAwait(false);
        }

        private async Task DeleteActivityInternalAsync(
            ConversationReference cr,
            IEnumerable<DeleteActivityHandler> deleteHandlers,
            Func<Task> callAtBottom,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(cr);

            if (deleteHandlers == null)
            {
                throw new ArgumentException($"{nameof(deleteHandlers)} is null", nameof(deleteHandlers));
            }

            // No middleware to run.
            if (!deleteHandlers.Any())
            {
                if (callAtBottom != null)
                {
                    await callAtBottom().ConfigureAwait(false);
                }

                return;
            }

            // Default to "No more Middleware after this".
            async Task NextAsync()
            {
                // Remove the first item from the list of middleware to call,
                // so that the next call just has the remaining items to worry about.
                IEnumerable<DeleteActivityHandler> remaining = deleteHandlers.Skip(1);
                await DeleteActivityInternalAsync(cr, remaining, callAtBottom, cancellationToken).ConfigureAwait(false);
            }

            // Grab the current middleware, which is the 1st element in the array, and execute it.
            DeleteActivityHandler toCall = deleteHandlers.First();
            await toCall(this, cr, NextAsync).ConfigureAwait(false);
        }
    }
}
