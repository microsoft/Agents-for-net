// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.BotBuilder
{
    /// <summary>
    /// Represents a bot adapter that can connect a bot to a service endpoint.
    /// This class is abstract.
    /// </summary>
    /// <remarks>The bot adapter encapsulates authentication processes and sends
    /// activities to and receives activities from the Bot Connector Service. When your
    /// bot receives an activity, the adapter creates a context object, passes it to your
    /// bot's application logic, and sends responses back to the user's channel.
    /// </remarks>
    /// <seealso cref="ITurnContext"/>
    /// <seealso cref="Activity"/>
    /// <seealso cref="IBot"/>
    public interface IChannelAdapter
    {
        /// <summary>
        /// Gets or sets an error handler that can catch exceptions in the middleware or application.
        /// </summary>
        /// <value>An error handler that can catch exceptions in the middleware or application.</value>
        Func<ITurnContext, Exception, Task> OnTurnError { get; set; }

        /// <summary>
        /// Gets the collection of middleware in the adapter's pipeline.
        /// </summary>
        /// <value>The middleware collection for the pipeline.</value>
        public IMiddlewareSet MiddlewareSet { get; }

        /// <summary>
        /// Adds middleware to the adapter's pipeline.
        /// </summary>
        /// <param name="middleware">The middleware to add.</param>
        /// <returns>The updated adapter object.</returns>
        /// <remarks>Middleware is added to the adapter at initialization time.
        /// For each turn, the adapter calls middleware in the order in which you added it.
        /// </remarks>
        IChannelAdapter Use(IMiddleware middleware);

        /// <summary>
        /// Creates a conversation on the specified channel.
        /// </summary>
        /// <param name="botAppId">TThe application ID of the bot.</param>
        /// <param name="channelId">The ID for the channel.</param>
        /// <param name="serviceUrl">The channel's service URL endpoint.</param>
        /// <param name="audience">The audience for the connector.</param>
        /// <param name="conversationParameters">The conversation information to use to
        /// create the conversation.</param>
        /// <param name="callback">The method to call for the resulting bot turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>To start a conversation, your bot must know its account information
        /// and the user's account information on that channel.
        /// Most _channels only support initiating a direct message (non-group) conversation.
        /// <para>The adapter attempts to create a new conversation on the channel, and
        /// then sends a <c>conversationUpdate</c> activity through its middleware pipeline
        /// to the <paramref name="callback"/> method.</para>
        /// <para>If the conversation is established with the
        /// specified users, the ID of the activity's <see cref="Activity.Conversation"/>
        /// will contain the ID of the new conversation.</para>
        /// </remarks>
        Task CreateConversationAsync(string botAppId, string channelId, string serviceUrl, string audience, ConversationParameters conversationParameters, BotCallbackHandler callback, CancellationToken cancellationToken);

        /// <summary>
        /// Sends a proactive message to a conversation.
        /// </summary>
        /// <param name="claimsIdentity">A <see cref="ClaimsIdentity"/> for the conversation.</param>
        /// <param name="continuationActivity">An <see cref="Activity"/> with the appropriate <see cref="ConversationReference"/> with which to continue the conversation.</param>
        /// <param name="callback">The method to call for the resulting bot turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>Call this method to proactively send a message to a conversation.
        /// Most _channels require a user to initiate a conversation with a bot
        /// before the bot can send activities to the user.</remarks>
        Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, BotCallbackHandler callback, CancellationToken cancellationToken);

        /// <summary>
        /// Sends a proactive message to a conversation.
        /// </summary>
        /// <param name="claimsIdentity">A <see cref="ClaimsIdentity"/> for the conversation.</param>
        /// <param name="continuationActivity">An <see cref="Activity"/> with the appropriate <see cref="ConversationReference"/> with which to continue the conversation.</param>
        /// <param name="audience">A value signifying the recipient of the proactive message.</param>
        /// <param name="callback">The method to call for the resulting bot turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>Call this method to proactively send a message to a conversation.
        /// Most _channels require a user to initiate a conversation with a bot
        /// before the bot can send activities to the user.</remarks>
        Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, string audience, BotCallbackHandler callback, CancellationToken cancellationToken);

        /// <summary>
        /// Sends a proactive message to a conversation.
        /// </summary>
        /// <param name="claimsIdentity">A <see cref="ClaimsIdentity"/> for the conversation.</param>
        /// <param name="reference">A reference to the conversation to continue.</param>
        /// <param name="callback">The method to call for the resulting bot turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>Call this method to proactively send a message to a conversation.
        /// Most _channels require a user to initiate a conversation with a bot
        /// before the bot can send activities to the user.</remarks>
        Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, ConversationReference reference, BotCallbackHandler callback, CancellationToken cancellationToken);

        /// <summary>
        /// Sends a proactive message to a conversation.
        /// </summary>
        /// <param name="claimsIdentity">A <see cref="ClaimsIdentity"/> for the conversation.</param>
        /// <param name="reference">A reference to the conversation to continue.</param>
        /// <param name="audience">A value signifying the recipient of the proactive message.</param>
        /// <param name="callback">The method to call for the resulting bot turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>Call this method to proactively send a message to a conversation.
        /// Most _channels require a user to initiate a conversation with a bot
        /// before the bot can send activities to the user.</remarks>
        Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, ConversationReference reference, string audience, BotCallbackHandler callback, CancellationToken cancellationToken);

        /// <summary>
        /// Sends a proactive message to a conversation.
        /// </summary>
        /// <param name="botId">The application ID of the bot. This parameter is ignored in
        /// single tenant the Adapters (Console, Test, etc) but is critical to the BotFrameworkAdapter
        /// which is multi-tenant aware. </param>
        /// <param name="continuationActivity">An <see cref="Activity"/> with the appropriate <see cref="ConversationReference"/> with which to continue the conversation.</param>
        /// <param name="callback">The method to call for the resulting bot turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>Call this method to proactively send a message to a conversation.
        /// Most _channels require a user to initiate a conversation with a bot
        /// before the bot can send activities to the user.</remarks>
        Task ContinueConversationAsync(string botId, IActivity continuationActivity, BotCallbackHandler callback, CancellationToken cancellationToken);

        /// <summary>
        /// Sends a proactive message to a conversation.
        /// </summary>
        /// <param name="botId">The application ID of the bot. This parameter is ignored in
        /// single tenant the Adapters (Console, Test, etc) but is critical to the BotFrameworkAdapter
        /// which is multi-tenant aware. </param>
        /// <param name="reference">A reference to the conversation to continue.</param>
        /// <param name="callback">The method to call for the resulting bot turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>Call this method to proactively send a message to a conversation.
        /// Most _channels require a user to initiate a conversation with a bot
        /// before the bot can send activities to the user.</remarks>
        Task ContinueConversationAsync(string botId, ConversationReference reference, BotCallbackHandler callback, CancellationToken cancellationToken);

        /// <summary>
        /// When overridden in a derived class, replaces an existing activity in the
        /// conversation.
        /// </summary>
        /// <param name="turnContext">The context object for the turn.</param>
        /// <param name="activity">New replacement activity.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>If the activity is successfully sent, the task result contains
        /// a <see cref="ResourceResponse"/> object containing the ID that the receiving
        /// channel assigned to the activity.
        /// <para>Before calling this, set the ID of the replacement activity to the ID
        /// of the activity to replace.</para></remarks>
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
        /// <returns>A task that represents the work queued to execute.</returns>
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
        /// <returns>A task that represents the work queued to execute.</returns>
        Task<InvokeResponse> ProcessActivityAsync(ClaimsIdentity claimsIdentity, IActivity activity, BotCallbackHandler callback, CancellationToken cancellationToken);

        /// <summary>
        /// When overridden in a derived class, sends activities to the conversation.
        /// </summary>
        /// <param name="turnContext">The context object for the turn.</param>
        /// <param name="activities">The activities to send.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>If the activities are successfully sent, the task result contains
        /// an array of <see cref="ResourceResponse"/> objects containing the IDs that
        /// the receiving channel assigned to the activities.</remarks>
        /// <seealso cref="ITurnContext.OnSendActivities(SendActivitiesHandler)"/>
        Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken);
    }
}