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
        /// Creates a conversation on the specified channel.
        /// </summary>
        /// <param name="agentAppId">TThe application ID of the Agent.</param>
        /// <param name="channelId">The ID for the channel.</param>
        /// <param name="serviceUrl">The channel's service URL endpoint.</param>
        /// <param name="audience">The audience for the connector.</param>
        /// <param name="conversationParameters">The conversation information to use to
        /// create the conversation.</param>
        /// <param name="callback">The method to call for the resulting Agent turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <remarks>To start a conversation, your Agent must know its account information
        /// and the user's account information on that channel.
        /// Most channels only support initiating a direct message (non-group) conversation.
        /// <para>The adapter attempts to create a new conversation on the channel, and
        /// then sends a <c>conversationUpdate</c> Activity through its middleware pipeline
        /// to the <paramref name="callback"/> method.</para>
        /// <para>If the conversation is established with the
        /// specified users, the ID of the activity's <see cref="Activity.Conversation"/>
        /// will contain the ID of the new conversation.</para>
        /// </remarks>
        Task CreateConversationAsync(string agentAppId, string channelId, string serviceUrl, string audience, ConversationParameters conversationParameters, AgentCallbackHandler callback, CancellationToken cancellationToken);

        /// <summary>
        /// Sends a proactive message to a conversation.
        /// </summary>
        /// <param name="claimsIdentity">A <see cref="ClaimsIdentity"/> for the conversation.</param>
        /// <param name="continuationActivity">An <see cref="Activity"/> with the appropriate <see cref="ConversationReference"/> with which to continue the conversation.</param>
        /// <param name="callback">The method to call for the resulting Agent turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>Call this method to proactively send a message to a conversation.
        /// Most channels require a user to initiate a conversation with an Agent
        /// before the Agent can send activities to the user.</remarks>
        Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, AgentCallbackHandler callback, CancellationToken cancellationToken);

        /// <summary>
        /// Sends a proactive message to a conversation.
        /// </summary>
        /// <param name="claimsIdentity">A <see cref="ClaimsIdentity"/> for the conversation.</param>
        /// <param name="continuationActivity">An <see cref="Activity"/> with the appropriate <see cref="ConversationReference"/> with which to continue the conversation.</param>
        /// <param name="audience">A value signifying the recipient of the proactive message.</param>
        /// <param name="callback">The method to call for the resulting Agent turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <remarks>Call this method to proactively send a message to a conversation.
        /// Most _channels require a user to initiate a conversation with an Agent
        /// before the Agent can send activities to the user.</remarks>
        Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, string audience, AgentCallbackHandler callback, CancellationToken cancellationToken);

        /// <summary>
        /// Sends a proactive message to a conversation.
        /// </summary>
        /// <param name="claimsIdentity">A <see cref="ClaimsIdentity"/> for the conversation.</param>
        /// <param name="reference">A reference to the conversation to continue.</param>
        /// <param name="callback">The method to call for the resulting Agent turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <remarks>Call this method to proactively send a message to a conversation.
        /// Most channels require a user to initiate a conversation with an Agent
        /// before the Agent can send activities to the user.</remarks>
        Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, ConversationReference reference, AgentCallbackHandler callback, CancellationToken cancellationToken);

        /// <summary>
        /// Sends a proactive message to a conversation.
        /// </summary>
        /// <param name="claimsIdentity">A <see cref="ClaimsIdentity"/> for the conversation.</param>
        /// <param name="reference">A reference to the conversation to continue.</param>
        /// <param name="audience">A value signifying the recipient of the proactive message.</param>
        /// <param name="callback">The method to call for the resulting Agent turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <remarks>Call this method to proactively send a message to a conversation.
        /// Most channels require a user to initiate a conversation with an Agent
        /// before the Agent can send activities to the user.</remarks>
        Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, ConversationReference reference, string audience, AgentCallbackHandler callback, CancellationToken cancellationToken);

        /// <summary>
        /// Sends a proactive message to a conversation.
        /// </summary>
        /// <param name="agentId">The application ID of the Agent.</param>
        /// <param name="continuationActivity">An <see cref="Activity"/> with the appropriate <see cref="ConversationReference"/> with which to continue the conversation.</param>
        /// <param name="callback">The method to call for the resulting Agent turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <remarks>Call this method to proactively send a message to a conversation.
        /// Most channels require a user to initiate a conversation with an Agent
        /// before the Agent can send activities to the user.</remarks>
        Task ContinueConversationAsync(string agentId, IActivity continuationActivity, AgentCallbackHandler callback, CancellationToken cancellationToken);

        /// <summary>
        /// Sends a proactive message to a conversation.
        /// </summary>
        /// <param name="agentId">The application ID of the Agent.</param>
        /// <param name="reference">A reference to the conversation to continue.</param>
        /// <param name="callback">The method to call for the resulting Agent turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <remarks>Call this method to proactively send a message to a conversation.
        /// Most _channels require a user to initiate a conversation with an Agent
        /// before the Agent can send activities to the user.</remarks>
        Task ContinueConversationAsync(string agentId, ConversationReference reference, AgentCallbackHandler callback, CancellationToken cancellationToken);

        /// <summary>
        /// When overridden in a derived class, replaces an existing activity in the
        /// conversation.
        /// </summary>
        /// <param name="turnContext">The context object for the turn.</param>
        /// <param name="activity">New replacement activity.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>If the activity is successfully sent, the task result contains
        /// a <see cref="ResourceResponse"/> object containing the ID that the receiving
        /// channel assigned to the activity.
        /// <para>Before calling this, set the ID of the replacement activity to the ID
        /// of the activity to replace.</para></returns>
        /// <seealso cref="ITurnContext.OnUpdateActivity(UpdateActivityHandler)"/>
        Task<ResourceResponse> UpdateActivityAsync(ITurnContext turnContext, IActivity activity, CancellationToken cancellationToken);

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
        Task DeleteActivityAsync(ITurnContext turnContext, ConversationReference reference, CancellationToken cancellationToken);

        /// <summary>
        /// Creates a turn context and runs the middleware pipeline for an incoming TRUSTED activity.
        /// </summary>
        /// <param name="claimsIdentity">A <see cref="ClaimsIdentity"/> for the request.</param>
        /// <param name="activity">The incoming activity.</param>
        /// <param name="callback">The code to run at the end of the adapter's middleware pipeline.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>If an Invoke Activity was received, an <see cref="InvokeResponse"/>, otherwise null.</returns>
        Task<InvokeResponse> ProcessActivityAsync(ClaimsIdentity claimsIdentity, IActivity activity, AgentCallbackHandler callback, CancellationToken cancellationToken);

        /// <summary>
        /// The implementation for continue conversation.
        /// </summary>
        /// <param name="claimsIdentity">A <see cref="ClaimsIdentity"/> for the conversation.</param>
        /// <param name="continuationActivity">The continuation <see cref="Activity"/> used to create the <see cref="ITurnContext" />.</param>
        /// <param name="audience">The audience for the call.</param>
        /// <param name="callback">The method to call for the resulting Agent turn.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ProcessProactiveAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, string audience, AgentCallbackHandler callback, CancellationToken cancellationToken);
        Task ProcessProactiveAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, IAgent agent, CancellationToken cancellationToken, string audience = null);

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
        Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken);
    }
}