﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Core.Interfaces
{
    /// <summary>
    /// A method that can participate in send activity events for the current turn.
    /// </summary>
    /// <param name="turnContext">The context object for the turn.</param>
    /// <param name="activities">The activities to send.</param>
    /// <param name="next">The delegate to call to continue event processing.</param>
    /// <returns>A task that represents the work queued to execute.</returns>
    /// <remarks>A handler calls the <paramref name="next"/> delegate to pass control to
    /// the next registered handler. If a handler doesn't call the next delegate,
    /// the adapter does not call any of the subsequent handlers and does not send the
    /// <paramref name="activities"/>.
    /// <para>If the activities are successfully sent, the <paramref name="next"/> delegate returns
    /// an array of <see cref="ResourceResponse"/> objects containing the IDs that
    /// the receiving channel assigned to the activities. Use this array as the return value of this handler.</para>
    /// </remarks>
    /// <seealso cref="IChannelAdapter"/>
    /// <seealso cref="ITurnContext.OnSendActivities(SendActivitiesHandler)"/>
    public delegate Task<ResourceResponse[]> SendActivitiesHandler(ITurnContext turnContext, List<IActivity> activities, Func<Task<ResourceResponse[]>> next);

    /// <summary>
    /// A method that can participate in update activity events for the current turn.
    /// </summary>
    /// <param name="turnContext">The context object for the turn.</param>
    /// <param name="activity">The replacement activity.</param>
    /// <param name="next">The delegate to call to continue event processing.</param>
    /// <returns>A task that represents the work queued to execute.</returns>
    /// <remarks>A handler calls the <paramref name="next"/> delegate to pass control to
    /// the next registered handler. If a handler doesn’t call the next delegate,
    /// the adapter does not call any of the subsequent handlers and does not update the
    /// activity.
    /// <para>The activity's <see cref="Activity.Id"/> indicates the activity in the
    /// conversation to replace.</para>
    /// <para>If the activity is successfully sent, the <paramref name="next"/> delegate returns
    /// a <see cref="ResourceResponse"/> object containing the ID that the receiving
    /// channel assigned to the activity. Use this response object as the return value of this handler.</para>
    /// </remarks>
    /// <seealso cref="IChannelAdapter"/>
    /// <seealso cref="SendActivitiesHandler"/>
    /// <seealso cref="DeleteActivityHandler"/>
    /// <seealso cref="ITurnContext.OnUpdateActivity(UpdateActivityHandler)"/>
    public delegate Task<ResourceResponse> UpdateActivityHandler(ITurnContext turnContext, IActivity activity, Func<Task<ResourceResponse>> next);

    /// <summary>
    /// A method that can participate in delete activity events for the current turn.
    /// </summary>
    /// <param name="turnContext">The context object for the turn.</param>
    /// <param name="reference">The conversation containing the activity.</param>
    /// <param name="next">The delegate to call to continue event processing.</param>
    /// <returns>A task that represents the work queued to execute.</returns>
    /// <remarks>A handler calls the <paramref name="next"/> delegate to pass control to
    /// the next registered handler. If a handler doesn’t call the next delegate,
    /// the adapter does not call any of the subsequent handlers and does not delete the
    /// activity.
    /// <para>The conversation reference's <see cref="ConversationReference.ActivityId"/>
    /// indicates the activity in the conversation to replace.</para>
    /// </remarks>
    /// <seealso cref="IChannelAdapter"/>
    /// <seealso cref="SendActivitiesHandler"/>
    /// <seealso cref="UpdateActivityHandler"/>
    /// <seealso cref="ITurnContext.OnDeleteActivity(DeleteActivityHandler)"/>
    public delegate Task DeleteActivityHandler(ITurnContext turnContext, ConversationReference reference, Func<Task> next);

    /// <summary>
    /// Provides context for a turn of a bot, where the context's <see cref="Activity"/> property is strongly typed.
    /// </summary>
    /// <typeparam name="T">The activity type for this turn of the bot.</typeparam>
    /// <remarks>The <see cref="IActivity"/> interface defines properties shared by every type of activity.
    /// The interfaces that derive from <see cref="IActivity"/> include properties specific to a specific
    /// <seealso cref="IActivity"/>
    /// <seealso cref="IBot"/>
    /// <seealso cref="ITurnContext"/>
    /// </remarks>
    public interface ITurnContext<T> : ITurnContext
        where T : IActivity
    {
        /// <summary>
        /// Gets the activity for this turn of the bot.
        /// </summary>
        /// <value>The activity for this turn of the bot.</value>
        new T Activity { get; }
    }

    /// <summary>
    /// Provides context for a turn of a bot.
    /// </summary>
    /// <remarks>Context provides information needed to process an incoming activity.
    /// The context object is created by a <see cref="IChannelAdapter"/> and persists for the
    /// length of the turn.</remarks>
    /// <seealso cref="IBot"/>
    /// <seealso cref="IMiddleware"/>
    public interface ITurnContext
    {
        /// <summary>
        /// Gets the bot adapter that created this context object.
        /// </summary>
        /// <value>The bot adapter that created this context object.</value>
        IChannelAdapter Adapter { get; }

        /// <summary>
        /// Gets the collection of values cached with the context object for the lifetime of the turn.
        /// </summary>
        /// <value>The collection of services registered on this context object.</value>
        TurnContextStateCollection TurnState { get; }

        /// <summary>
        /// Gets the activity for this turn of the bot.
        /// </summary>
        /// <value>The activity for this turn of the bot.</value>
        IActivity Activity { get; }

        /// <summary>
        /// Gets a value indicating whether at least one response was sent for the current turn.
        /// </summary>
        /// <value><c>true</c> if at least one response was sent for the current turn; otherwise, <c>false</c>.</value>
        /// <seealso cref="SendActivityAsync(IActivity, CancellationToken)"/>
        bool Responded { get; }

        /// <summary>
        /// Sends a message activity to the sender of the incoming activity.
        /// </summary>
        /// <param name="textReplyToSend">The text of the message to send.</param>
        /// <param name="speak">Optional, text to be spoken by your bot on a speech-enabled
        /// channel.</param>
        /// <param name="inputHint">Optional, indicates whether your bot is accepting,
        /// expecting, or ignoring user input after the message is delivered to the client.
        /// <see cref="InputHints"/> defines the possible values.
        /// Default is <see cref="InputHints.AcceptingInput"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>If the activity is successfully sent, the task result contains
        /// a <see cref="ResourceResponse"/> object that contains the ID that the receiving
        /// channel assigned to the activity.
        /// <para>See the channel's documentation for limits imposed upon the contents of
        /// <paramref name="textReplyToSend"/>.</para>
        /// <para>To control various characteristics of your bot's speech such as voice,
        /// rate, volume, pronunciation, and pitch, specify <paramref name="speak"/> in
        /// Speech Synthesis Markup Language (SSML) format.</para>
        /// </remarks>
        /// <seealso cref="OnSendActivities(SendActivitiesHandler)"/>
        ///// <seealso cref="SendActivityAsync(IActivity, CancellationToken)"/>
        ///// <seealso cref="SendActivitiesAsync(IActivity[], CancellationToken)"/>
        Task<ResourceResponse> SendActivityAsync(string textReplyToSend, string speak = null, string inputHint = "acceptingInput", CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends an activity to the sender of the incoming activity.
        /// </summary>
        /// <param name="activity">The activity to send.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>If the activity is successfully sent, the task result contains
        /// a <see cref="ResourceResponse"/> object containing the ID that the receiving
        /// channel assigned to the activity.</remarks>
        /// <seealso cref="OnSendActivities(SendActivitiesHandler)"/>
        /// <seealso cref="SendActivityAsync(string, string, string, CancellationToken)"/>
        /// <seealso cref="SendActivitiesAsync(IActivity[], CancellationToken)"/>
        Task<ResourceResponse> SendActivityAsync(IActivity activity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a set of activities to the sender of the incoming activity.
        /// </summary>
        /// <param name="activities">The activities to send.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>If the activities are successfully sent, the task result contains
        /// an array of <see cref="ResourceResponse"/> objects containing the IDs that
        /// the receiving channel assigned to the activities.</remarks>
        /// <seealso cref="OnSendActivities(SendActivitiesHandler)"/>
        /// <seealso cref="SendActivityAsync(string, string, string, CancellationToken)"/>
        /// <seealso cref="SendActivityAsync(IActivity, CancellationToken)"/>
        Task<ResourceResponse[]> SendActivitiesAsync(IActivity[] activities, CancellationToken cancellationToken = default);

        /// <summary>
        /// Replaces an existing activity.
        /// </summary>
        /// <param name="activity">New replacement activity.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>If the activity is successfully sent, the task result contains
        /// a <see cref="ResourceResponse"/> object containing the ID that the receiving
        /// channel assigned to the activity.
        /// <para>Before calling this, set the ID of the replacement activity to the ID
        /// of the activity to replace.</para>
        /// <para>Not all channels support this operation. For channels that don't, this call may throw an exception.</para></remarks>
        /// <seealso cref="OnUpdateActivity(UpdateActivityHandler)"/>
        /// <seealso cref="SendActivitiesAsync(IActivity[], CancellationToken)"/>
        /// <seealso cref="DeleteActivityAsync(ConversationReference, CancellationToken)"/>
        Task<ResourceResponse> UpdateActivityAsync(IActivity activity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes an existing activity.
        /// </summary>
        /// <param name="activityId">The ID of the activity to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <para>Not all channels support this operation. For channels that don\'t, this call may throw an exception.</para>
        /// <seealso cref="OnDeleteActivity(DeleteActivityHandler)"/>
        /// <seealso cref="DeleteActivityAsync(ConversationReference, CancellationToken)"/>
        /// <seealso cref="SendActivitiesAsync(IActivity[], CancellationToken)"/>
        /// <seealso cref="UpdateActivityAsync(IActivity, CancellationToken)"/>
        Task DeleteActivityAsync(string activityId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes an existing activity.
        /// </summary>
        /// <param name="conversationReference">The conversation containing the activity to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>The conversation reference's <see cref="ConversationReference.ActivityId"/>
        /// indicates the activity in the conversation to delete.
        /// <para>Not all channels support this operation. For channels that don't, this call may throw an exception.</para></remarks>
        /// <seealso cref="OnDeleteActivity(DeleteActivityHandler)"/>
        /// <seealso cref="DeleteActivityAsync(string, CancellationToken)"/>
        /// <seealso cref="SendActivitiesAsync(IActivity[], CancellationToken)"/>
        /// <seealso cref="UpdateActivityAsync(IActivity, CancellationToken)"/>
        Task DeleteActivityAsync(ConversationReference conversationReference, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a response handler for send activity operations.
        /// </summary>
        /// <param name="handler">The handler to add to the context object.</param>
        /// <returns>The updated context object.</returns>
        /// <remarks>When the context's <see cref="SendActivityAsync(IActivity, CancellationToken)"/>
        /// or <see cref="SendActivitiesAsync(IActivity[], CancellationToken)"/> method is called,
        /// the adapter calls the registered handlers in the order in which they were
        /// added to the context object.
        /// </remarks>
        /// <seealso cref="SendActivityAsync(string, string, string, CancellationToken)"/>
        /// <seealso cref="SendActivityAsync(IActivity, CancellationToken)"/>
        /// <seealso cref="SendActivitiesAsync(IActivity[], CancellationToken)"/>
        /// <seealso cref="SendActivitiesHandler"/>
        ITurnContext OnSendActivities(SendActivitiesHandler handler);

        /// <summary>
        /// Adds a response handler for update activity operations.
        /// </summary>
        /// <param name="handler">The handler to add to the context object.</param>
        /// <returns>The updated context object.</returns>
        /// <remarks>When the context's <see cref="UpdateActivityAsync(IActivity, CancellationToken)"/> is called,
        /// the adapter calls the registered handlers in the order in which they were
        /// added to the context object.
        /// </remarks>
        /// <seealso cref="UpdateActivityAsync(IActivity, CancellationToken)"/>
        /// <seealso cref="UpdateActivityHandler"/>
        /// <seealso cref="OnSendActivities(SendActivitiesHandler)"/>
        /// <seealso cref="OnDeleteActivity(DeleteActivityHandler)"/>
        ITurnContext OnUpdateActivity(UpdateActivityHandler handler);

        /// <summary>
        /// Adds a response handler for delete activity operations.
        /// </summary>
        /// <param name="handler">The handler to add to the context object.</param>
        /// <returns>The updated context object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <c>null</c>.</exception>
        /// <remarks>When the context's <see cref="DeleteActivityAsync(string, CancellationToken)"/> is called,
        /// the adapter calls the registered handlers in the order in which they were
        /// added to the context object.
        /// </remarks>
        /// <seealso cref="DeleteActivityAsync(ConversationReference, CancellationToken)"/>
        /// <seealso cref="DeleteActivityAsync(string, CancellationToken)"/>
        /// <seealso cref="DeleteActivityHandler"/>
        /// <seealso cref="OnSendActivities(SendActivitiesHandler)"/>
        /// <seealso cref="OnUpdateActivity(UpdateActivityHandler)"/>
        ITurnContext OnDeleteActivity(DeleteActivityHandler handler);


        /// <summary>
        /// Sends a trace activity to the <see cref="IChannelAdapter"/> for logging purposes.
        /// </summary>
        /// <param name="turnContext">The context for the current turn.</param>
        /// <param name="name">The value to assign to the activity's <see cref="Activity.Name"/> property.</param>
        /// <param name="value">The value to assign to the activity's <see cref="Activity.Value"/> property.</param>
        /// <param name="valueType">The value to assign to the activity's <see cref="Activity.ValueType"/> property.</param>
        /// <param name="label">The value to assign to the activity's <see cref="Activity.Label"/> property.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>If the adapter is being hosted in the Emulator, the task result contains
        /// a <see cref="ResourceResponse"/> object with the original trace activity's ID; otherwise,
        /// it contains a <see cref="ResourceResponse"/> object containing the ID that the receiving
        /// channel assigned to the activity.</remarks>
        Task<ResourceResponse> TraceActivityAsync(string name, object value = null, string valueType = null, [CallerMemberName] string label = null, CancellationToken cancellationToken = default);
    }
}
