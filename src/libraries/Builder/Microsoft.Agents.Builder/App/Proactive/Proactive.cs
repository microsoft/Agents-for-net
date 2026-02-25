// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App.Proactive
{
    /// <summary>
    /// Provides methods for storing, retrieving, and managing conversation references to enable proactive messaging
    /// scenarios. Supports sending activities and continuing conversations outside the standard request/response flow
    /// using stored conversation references.
    /// </summary>
    /// <remarks>Use the Proactive class to implement scenarios where aan agent needs to initiate conversations or
    /// send messages to users without an incoming activity, such as notifications or scheduled alerts. Some operations
    /// require that Conversation references be stored using StoreConversationAsync before they can be used.
    /// </remarks>
    public class Proactive
    {
        private readonly AgentApplication _app;
        private readonly ProactiveOptions _options;

        /// <summary>
        /// <c>IAcitivty.ValueType</c> that indicates additional key/values for the ContinueConversation Event.
        /// </summary>
        public static readonly string ContinueConversationValueType = "application/vnd.microsoft.activity.continueconversation";

        public Proactive(AgentApplication app)
        {
            _app = app;
            _options = app.Options.Proactive;
        }

        /// <summary>
        /// Sends an activity to an existing conversation using the specified channel adapter.
        /// </summary>
        /// <param name="adapter">The channel adapter used to send the activity. Cannot be null.</param>
        /// <param name="conversationId">The unique identifier of the conversation to which the activity will be sent. Cannot be 
        /// null or empty. The conversation must have been stored using <see cref="StoreConversationAsync(ITurnContext, CancellationToken)"/></param>
        /// <param name="activity">The activity to send to the conversation. Must not be null. If the activity's Type property is null or
        /// empty, it defaults to a message activity.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the send operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a ResourceResponse with the ID
        /// of the sent activity.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if no conversation reference is found for the specified conversation ID.</exception>
        public async Task<ResourceResponse> SendActivityAsync(IChannelAdapter adapter, string conversationId, IActivity activity, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(conversationId, nameof(conversationId));
            AssertionHelpers.ThrowIfNull(activity, nameof(activity));

            if (string.IsNullOrEmpty(activity.Type))
            {
                activity.Type = ActivityTypes.Message;
            }

            var conversation = await GetConversationAsync(conversationId, cancellationToken).ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"No conversation reference found for conversation ID '{conversationId}'.");

            return await SendActivityAsync(adapter, conversation, activity, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends an activity to a conversation using the specified channel adapter and conversation reference.
        /// </summary>
        /// <param name="adapter">The channel adapter used to send the activity. Cannot be null.</param>
        /// <param name="conversation">Instance of a <c>Conversation</c>.  This can be created with <see cref="Conversation"/> constructors or <see cref="ConversationBuilder"/>.</param>
        /// <param name="activity">The activity to send to the conversation. If the activity's Type property is null or empty, it defaults to a
        /// message activity. Cannot be null.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the send operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a ResourceResponse with
        /// information about the sent activity.</returns>
        public static async Task<ResourceResponse> SendActivityAsync(IChannelAdapter adapter, Conversation conversation, IActivity activity, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(adapter, nameof(adapter));
            AssertionHelpers.ThrowIfNull(activity, nameof(activity));
            AssertionHelpers.ThrowIfNull(conversation, nameof(conversation));

            if (string.IsNullOrEmpty(activity.Type))
            {
                activity.Type = ActivityTypes.Message;
            }

            ResourceResponse response = null;
            await adapter.ContinueConversationAsync(conversation.Identity, conversation.Reference, async (turnContext, ct) =>
            {
                response = await turnContext.SendActivityAsync(activity, ct).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);

            return response;
        }

        /// <summary>
        /// Continues an existing conversation by resuming activity using the specified channel adapter and conversation
        /// ID. The conversation must have previously been stored using <see cref="StoreConversationAsync(ITurnContext, CancellationToken)"/>.<br/><br/>
        /// See  <see cref="ContinueConversationAsync(IChannelAdapter, Conversation, RouteHandler, IActivity, string[], CancellationToken)"/> 
        /// for more details.
        /// </summary>
        /// <param name="adapter">The channel adapter used to send and receive activities for the conversation.</param>
        /// <param name="conversationId">The unique identifier of the conversation to continue. Cannot be null or empty.</param>
        /// <param name="continuationHandler">A delegate that handles the routing of activities within the continued conversation.</param>
        /// <param name="continuationActivity">Optional.  If null the default continuation activity of type Event and name "ContinueConversation" is used.</param>
        /// <param name="tokenHandlers">Optional: The list of tokens to get.  If a handler requires sign-in, only those that have done that can be returned.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if no conversation reference is found for the specified conversation ID.</exception>
        public async Task ContinueConversationAsync(IChannelAdapter adapter, string conversationId, RouteHandler continuationHandler, IActivity continuationActivity = null, string[] tokenHandlers = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(conversationId, nameof(conversationId));

            var conversation = await GetConversationAsync(conversationId, cancellationToken).ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"No conversation reference found for conversation ID '{conversationId}'.");
            await ContinueConversationAsync(adapter, conversation, continuationHandler, continuationActivity, tokenHandlers, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Continues an existing conversation by calling the specified route handler within the context of the
        /// provided conversation reference.  This method will provide TurnContext and TurnState relative to the 
        /// original conversation.
        /// </summary>
        /// <remarks>
        /// Add a ContinueConversation handler in your AgentApplication to handle incoming requests to continue conversations.  For example:
        /// <code>
        /// public class MyProactiveAgent : AgentApplication 
        /// {
        ///     [ContinueConversation]
        ///     public async Task HandleContinueConversationAsync(ITurnContext turnContext, TurnState turnState, CancellationToken cancellationToken)
        ///     {
        ///         // ITurnContext and TurnState will be relative to the original conversation, allowing you to continue the conversation as 
        ///         // if you were responding to an incoming activity.
        ///     }
        /// }
        /// </code>
        /// 
        /// In Program.cs, map configured ContinueConversation handlers:
        /// <code>
        /// app.MapAgentProactiveEndpoints&lt;MyProactiveAgent&gt;();
        /// </code>
        /// </remarks>
        /// <param name="adapter">The channel adapter used to continue the conversation. Must not be null.</param>
        /// <param name="conversation">Instance of a <c>Conversation</c>.  This can be created with <see cref="Conversation"/> constructors or <see cref="ConversationBuilder"/>.</param>
        /// <param name="continuationHandler">The route handler delegate to execute within the continued conversation context. Must not be null.</param>
        /// <param name="continuationActivity">Optional.  If null the default continuation activity of type Event and name "ContinueConversation" is used.</param>
        /// <param name="tokenHandlers">Optional: The list of tokens to get.  If a handler requires sign-in, only those that have done that can be returned.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task ContinueConversationAsync(IChannelAdapter adapter, Conversation conversation, RouteHandler continuationHandler, IActivity continuationActivity = null, string[] tokenHandlers = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(adapter, nameof(adapter));
            AssertionHelpers.ThrowIfNull(conversation, nameof(conversation));
            AssertionHelpers.ThrowIfNull(continuationHandler, nameof(continuationHandler));

            continuationActivity ??= conversation.Reference.GetContinuationActivity();

            await adapter.ProcessProactiveAsync(conversation.Identity, continuationActivity, null, async (turnContext, ct) =>
            {
                await OnTurnAsync(turnContext, continuationHandler, tokenHandlers: tokenHandlers, ct).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a new conversation using the specified channel adapter and conversation information.
        /// </summary>
        /// <param name="adapter">The channel adapter used to create the conversation. Cannot be null.</param>
        /// <param name="createInfo">An object containing the details required to create the conversation, including conversation identity,
        /// reference, parameters, and scope. Cannot be null. See <see cref="CreateConversationBuilder"/>.</param>
        /// <param name="continuationHandler">If null a ContinueConversation is not performed after the conversation is created.</param>
        /// <param name="continuationActivityFactory">Optional.  If not supplied, the default activity of type Event and name "CreateConversation" is used.</param>
        /// <param name="tokenHandlers">Optional: The list of tokens to get.  If a handler requires sign-in, only those that have done that can be returned.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a reference to the newly created
        /// conversation.</returns>
        public async Task<ConversationReference> CreateConversationAsync(
            IChannelAdapter adapter, 
            CreateConversation createInfo, 
            RouteHandler continuationHandler = null, 
            Func<ConversationReference, IActivity> continuationActivityFactory = null,
            string[] tokenHandlers = null,
            CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(adapter, nameof(adapter));
            AssertionHelpers.ThrowIfNull(createInfo, nameof(createInfo));

            var newReference = await adapter.CreateConversationAsync(
                createInfo.Conversation.Identity,
                createInfo.Conversation.Reference,
                createInfo.Parameters,
                createInfo.Scope,
                null,
                cancellationToken).ConfigureAwait(false);

            AgentCallbackHandler continuation = continuationHandler != null ? (context, ct) => OnTurnAsync(context, continuationHandler, tokenHandlers: tokenHandlers, ct) : null;
            if (continuation != null)
            {
                var continuationActivity = continuationActivityFactory != null ? continuationActivityFactory(newReference) : newReference.GetCreateContinuationActivity();
                await adapter.ProcessProactiveAsync(createInfo.Conversation.Identity, continuationActivity, null, continuation, cancellationToken).ConfigureAwait(false);
            }

            return newReference;
        }

        #region Conversation Storage
        /// <summary>
        /// Stores the current conversation reference in the proactive storage from the ITurnContext.
        /// </summary>
        /// <remarks>Use this method to enable proactive messaging scenarios by persisting conversation
        /// references. The returned conversation identifier can be used to retrieve or reference the conversation in
        /// future operations.</remarks>
        /// <param name="turnContext">The context object for the current turn, containing activity and conversation information. Cannot be null.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A string containing the conversation identifier for the stored conversation reference.</returns>
        public Task<string> StoreConversationAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
            return StoreConversationAsync(new Conversation(turnContext), cancellationToken);
        }

        /// <summary>
        /// Stores the current conversation reference in the proactive storage and returns the conversation identifier.
        /// </summary>
        /// <param name="conversation">The conversation reference record to store. Cannot be null.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the ID of the stored
        /// conversation.</returns>
        public async Task<string> StoreConversationAsync(Conversation conversation, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(conversation, nameof(conversation));

            var key = GetRecordKey(conversation.Reference.Conversation.Id);
            await _app.Options.Proactive.Storage.WriteAsync(
                new Dictionary<string, object>
                {
                    { key, conversation }
                },
                cancellationToken).ConfigureAwait(false);
            return conversation.Reference.Conversation.Id;
        }

        /// <summary>
        /// Retrieves the conversation reference record associated with the specified conversation identifier
        /// asynchronously.
        /// </summary>
        /// <remarks>If no conversation reference exists for the specified identifier, the method returns
        /// <see langword="null"/>. This method is thread-safe and does not modify storage.</remarks>
        /// <param name="conversationId">The unique identifier of the conversation for which to retrieve the reference record. Cannot be null or
        /// empty.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A <see cref="Conversation"/> representing the conversation reference if found; otherwise,
        /// <see langword="null"/>.</returns>
        public async Task<Conversation?> GetConversationAsync(string conversationId, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(conversationId, nameof(conversationId));

            var key = GetRecordKey(conversationId);
            var items = await _options.Storage.ReadAsync([key], cancellationToken).ConfigureAwait(false);

            if (items != null && items.TryGetValue(key, out var item) && item is Conversation record)
            {
                return record;
            }
            return null;
        }

        /// <summary>
        /// Deletes the conversation reference associated with the specified conversation ID from persistent storage
        /// asynchronously.
        /// </summary>
        /// <remarks>If the conversation reference does not exist for the specified conversation ID, no
        /// action is taken. This method is thread-safe and can be called concurrently.</remarks>
        /// <param name="conversationId">The unique identifier of the conversation whose reference is to be deleted. Cannot be null or empty.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the delete operation.</param>
        /// <returns>A task that represents the asynchronous delete operation.</returns>
        public Task DeleteConversationAsync(string conversationId, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(conversationId, nameof(conversationId));
            var key = GetRecordKey(conversationId);
            return _options.Storage.DeleteAsync([key], cancellationToken);
        }

        private async Task OnTurnAsync(ITurnContext turnContext, RouteHandler handler, string[] tokenHandlers = null, CancellationToken cancellationToken = default)
        {
            var turnState = _app.Options.TurnStateFactory!();
            await turnState.LoadStateAsync(turnContext, false, cancellationToken).ConfigureAwait(false);

            try
            {
                if (tokenHandlers?.Length > 0 && _app.UserAuthorization != null)
                {
                    turnContext.Services.Set<UserAuthorization>(_app.UserAuthorization);
                    await _app.UserAuthorization.GetSignedInTokensAsync(turnContext, tokenHandlers, cancellationToken).ConfigureAwait(false);
                }

                await handler(turnContext, turnState, cancellationToken).ConfigureAwait(false);

                await turnState.SaveStateAsync(turnContext, false, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (turnContext.StreamingResponse != null && turnContext.StreamingResponse.IsStreamStarted())
                {
                    await turnContext.StreamingResponse.EndStreamAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private static string GetRecordKey(string conversationId)
        {
            return $"conversationreferences/{conversationId}";
        }
        #endregion
    }
}
