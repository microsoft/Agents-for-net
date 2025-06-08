// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Represents an Adapter that can connect an Agent to a service endpoint.
    /// </summary>
    /// <remarks>The Adapter encapsulates processing a received Activity, creates an
    /// <see cref="ITurnContext"/> and calls <see cref="IAgent.OnTurnAsync(ITurnContext, CancellationToken)"/>. 
    /// When your Agent receives an activity, response are sent to the caller via <see cref="ITurnContext.SendActivityAsync(IActivity, CancellationToken)"/>.
    /// </remarks>
    /// <seealso cref="ITurnContext"/>
    /// <seealso cref="IActivity"/>
    /// <seealso cref="IAgent"/>
    public interface IChannelAdapter
    {
        /// <summary>
        /// Gets or sets an error handler that can catch exceptions in the middleware or application.
        /// </summary>
        /// <value>An error handler that can catch exceptions in the middleware or application.</value>
        Func<ITurnContext, Exception, Task> OnTurnError { get; set; }

        /// <summary>
        /// Gets the collection of middleware in the Adapter's pipeline.
        /// </summary>
        /// <value>The middleware collection for the pipeline.</value>
        public IMiddlewareSet MiddlewareSet { get; }

        /// <summary>
        /// Adds middleware to the adapter's pipeline.
        /// </summary>
        /// <param name="middleware">The middleware to add.</param>
        /// <returns>The updated IChannelAdapter object.</returns>
        /// <remarks>Middleware is added to the adapter at initialization time.
        /// For each turn, the adapter calls middleware in the order in which you added it.
        /// </remarks>
        IChannelAdapter Use(IMiddleware middleware);

        /// <summary>
        /// Creates a conversation on the current channel.
        /// </summary>
        /// <param name="turnContext"></param>
        /// <param name="conversationParameters">The conversation information to use to create the conversation.</param>
        /// <param name="channelId">If null, the ITurnContext.Activity.ChannelId is used.</param>
        /// <param name="cancellationToken"></param>
        Task<ConversationReference> CreateConversationAsync(ITurnContext turnContext, ConversationParameters conversationParameters, string channelId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a conversation on the current channel.
        /// </summary>
        /// <param name="claimsIdentity"></param>
        /// <param name="reference"></param>
        /// <param name="callback"></param>
        /// <param name="cancellationToken"></param>
        Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, ConversationReference reference, AgentCallbackHandler callback, CancellationToken cancellationToken = default);

        /// <summary>
        /// When overridden in a derived class, replaces an existing activity in the
        /// conversation.
        /// </summary>
        /// <param name="turnContext">The context object for the turn.</param>
        /// <param name="activity">New replacement activity.  The Id of this Activity should be the Id of the Activity to update.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>If the activity is successfully sent, the task result contains
        /// a <see cref="ResourceResponse"/> object containing the ID that the receiving
        /// channel assigned to the activity.</returns>
        Task<ResourceResponse> UpdateActivityAsync(ITurnContext turnContext, IActivity activity, CancellationToken cancellationToken = default);

        /// <summary>
        /// When overridden in a derived class, deletes an existing activity in the
        /// conversation.
        /// </summary>
        /// <param name="turnContext">The context object for the turn.</param>
        /// <param name="reference">Conversation reference for the activity to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <remarks>The <see cref="ConversationReference.ActivityId"/> of the conversation
        /// reference identifies the activity to delete.</remarks>
        /// <seealso cref="ITurnContext.OnDeleteActivity(DeleteActivityHandler)"/>
        Task DeleteActivityAsync(ITurnContext turnContext, ConversationReference reference, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a turn context and runs the middleware pipeline for an incoming TRUSTED activity.
        /// </summary>
        /// <param name="claimsIdentity">A <see cref="ClaimsIdentity"/> for the request.</param>
        /// <param name="activity">The incoming activity.</param>
        /// <param name="callback">The code to run at the end of the adapter's middleware pipeline.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>If an Invoke Activity was received, an <see cref="InvokeResponse"/>, otherwise null.</returns>
        Task<InvokeResponse> ProcessActivityAsync(ClaimsIdentity claimsIdentity, IActivity activity, AgentCallbackHandler callback, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates and executes a new Activity Pipeline with an Activity.
        /// </summary>
        /// <param name="claimsIdentity">A <see cref="ClaimsIdentity"/> for the conversation.</param>
        /// <param name="continuationActivity">This Activity populates the ITurnContext.Activity value.</param>
        /// <param name="callback">The method to call for the resulting Agent turn.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ProcessProactiveAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, AgentCallbackHandler callback, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates runs a new Activity Pipeline with an Activity.
        /// </summary>
        /// <param name="claimsIdentity">A <see cref="ClaimsIdentity"/> for the conversation.</param>
        /// <param name="continuationActivity">This Activity populates the ITurnContext.Activity value.</param>
        /// <param name="agent"></param>
        /// <param name="callback">The method to call for the resulting Agent turn.</param>
        /// <param name="audience">The audience for the call.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ProcessProactiveAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, IAgent agent, CancellationToken cancellationToken = default);

        /// <summary>
        /// When overridden in a derived class, sends activities to the conversation.
        /// </summary>
        /// <param name="turnContext">The context object for the turn.</param>
        /// <param name="activities">The activities to send.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>If the activities are successfully sent, the task result contains
        /// an array of <see cref="ResourceResponse"/> objects containing the IDs that
        /// the receiving channel assigned to the activities.</returns>
        /// <seealso cref="ITurnContext.OnSendActivities(SendActivitiesHandler)"/>
        Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken = default);
    }
}