// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App.Proactive
{
    public class Proactive
    {
        private readonly AgentApplication _app;
        private readonly ProactiveOptions _options;

        /// <summary>
        /// Options <c>IAcitivty.ValueType</c> that indicates additional information for the ContinueConversation Event. If
        /// specified, the AgentApplication can use this to route the Event to a RouteHandler.
        /// </summary>
        public static readonly string ContinueConversationValueType = "application/vnd.microsoft.activity.continueconversation";

        public Proactive(AgentApplication app)
        {
            _app = app;
            _options = app.Options.Proactive;
        }

        // TODO: CreateConversation method to start new conversations proactively

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
        public async Task<ResourceResponse> SendActivityAsync(IChannelAdapter adapter, string conversationId, IActivity activity, CancellationToken cancellationToken)
        {
            AssertionHelpers.ThrowIfNull(activity, nameof(activity));

            if (string.IsNullOrEmpty(activity.Type))
            {
                activity.Type = ActivityTypes.Message;
            }

            var record = await GetConversationReferenceAsync(conversationId, cancellationToken).ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"No conversation reference found for conversation ID '{conversationId}'.");

            return await SendActivityAsync(adapter, record, activity, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends an activity to a conversation using the specified channel adapter and conversation reference.
        /// </summary>
        /// <param name="adapter">The channel adapter used to send the activity. Cannot be null.</param>
        /// <param name="record">The conversation reference record containing the identity and conversation information. Cannot be null.</param>
        /// <param name="activity">The activity to send to the conversation. If the activity's Type property is null or empty, it defaults to a
        /// message activity. Cannot be null.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the send operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a ResourceResponse with
        /// information about the sent activity.</returns>
        public static async Task<ResourceResponse> SendActivityAsync(IChannelAdapter adapter, ConversationReferenceRecord record, IActivity activity, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(activity, nameof(activity));
            AssertionHelpers.ThrowIfNull(record, nameof(record));

            if (string.IsNullOrEmpty(activity.Type))
            {
                activity.Type = ActivityTypes.Message;
            }

            ResourceResponse response = null;
            await adapter.ContinueConversationAsync(record.Identity, record.Reference, async (turnContext, ct) =>
            {
                response = await turnContext.SendActivityAsync(activity, ct).ConfigureAwait(false);
            }, cancellationToken);

            return response;
        }

        /// <summary>
        /// Continues an existing conversation by resuming activity using the specified channel adapter and conversation
        /// ID. The conversation must have previously been stored using <see cref="StoreConversationAsync(ITurnContext, CancellationToken)"/>.<br/><br/>
        /// 
        /// See <see cref="ContinueConversationAsync(IChannelAdapter, ClaimsIdentity, ConversationReference, RouteHandler, CancellationToken)"/>.
        /// </summary>
        /// <param name="adapter">The channel adapter used to send and receive activities for the conversation.</param>
        /// <param name="conversationId">The unique identifier of the conversation to continue. Cannot be null or empty.</param>
        /// <param name="handler">A delegate that handles the routing of activities within the continued conversation.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if no conversation reference is found for the specified conversation ID.</exception>
        public async Task ContinueConversationAsync(IChannelAdapter adapter, string conversationId, RouteHandler handler, CancellationToken cancellationToken = default)
        {
            var record = await GetConversationReferenceAsync(conversationId, cancellationToken).ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"No conversation reference found for conversation ID '{conversationId}'.");
            await ContinueConversationAsync(adapter, record.Identity, record.Reference, handler, cancellationToken);
        }

        /// <summary>
        /// Continues an existing conversation by invoking the specified route handler within the context of the
        /// provided conversation reference.  This method will provide TurnContext and TurnState relative to the 
        /// original conversation context, allowing the route handler to process activities and operations as if 
        /// they were occurring within the original conversation. After the route handler completes, any changes 
        /// to the TurnState will be saved back to the underlying storage.
        /// </summary>
        /// <remarks>NOTE:  OAuth is not availble within the turn when using this.</remarks>
        /// <param name="adapter">The channel adapter used to continue the conversation. Must not be null.</param>
        /// <param name="identity">The claims identity representing the user or bot on whose behalf the conversation is continued. Must not be
        /// null.</param>
        /// <param name="reference">The conversation reference that identifies the conversation to continue. Must not be null.</param>
        /// <param name="handler">The route handler delegate to execute within the continued conversation context. Must not be null.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task ContinueConversationAsync(IChannelAdapter adapter, ClaimsIdentity identity, ConversationReference reference, RouteHandler handler, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(adapter, nameof(adapter));
            AssertionHelpers.ThrowIfNull(identity, nameof(identity));
            AssertionHelpers.ThrowIfNull(reference, nameof(reference));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            await adapter.ContinueConversationAsync(identity, reference, async (turnContext, ct) =>
            {
                var turnState = _app.Options.TurnStateFactory!();
                await turnState.LoadStateAsync(turnContext, false, ct).ConfigureAwait(false);

                await handler(turnContext, turnState, ct).ConfigureAwait(false);

                await turnState.SaveStateAsync(turnContext, false, ct).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Continues an existing conversation by starting proactive turn.  <see cref="AddContinueConversationRoute(RouteHandler, string[], RouteSelector)"/>
        /// MUST have been called to register a route handler for the <c>ContinueConversation</c> Event prior to calling this method. The registered route 
        /// handler will be called with the same TurnState in the context of the original conversation, and if specified on the route the OAuth tokens.
        /// </summary>
        /// <param name="adapter">The channel adapter used to process the proactive activity. Cannot be null.</param>
        /// <param name="conversationId">The unique identifier of the conversation to continue. Cannot be null or empty.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if no conversation reference is found for the specified conversation ID.  The 
        /// conversation should have originally been stored using <see cref="StoreConversationAsync(ITurnContext, CancellationToken)"/></exception>
        public async Task ContinueConversationAsync(IChannelAdapter adapter, string conversationId, CancellationToken cancellationToken = default)
        {
            var record = await GetConversationReferenceAsync(conversationId, cancellationToken).ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"No conversation reference found for conversation ID '{conversationId}'.");
            await adapter.ProcessProactiveAsync(record!.Identity, record.Reference!.GetContinuationActivity(), _app, cancellationToken);
        }

        /// <summary>
        /// Continues an existing conversation by starting proactive turn.  <see cref="AddContinueConversationRoute(RouteHandler, string[], RouteSelector)"/>
        /// MUST have been called to register a route handler for the <c>ContinueConversation</c> Event prior to calling this method. The registered route 
        /// handler will be called with the same TurnState in the context of the original conversation, and if specified on the route the OAuth tokens.
        /// </summary>
        /// <param name="adapter">The channel adapter used to process the proactive conversation activity.</param>
        /// <param name="conversationId">The unique identifier of the conversation to continue. Must correspond to a valid conversation reference.</param>
        /// <param name="continueProperties">A dictionary containing properties to include in the continuation activity. These properties are passed as
        /// the activity's value.  These can be used in a custom RouteSelector to route to the desired RouteHandler.<br/><br/>The ITurnContext.Activity.Value 
        /// will contain the dictionary. The ITurnContext.Activity.ValueType will be <see cref="ContinueConversationValueType"/></param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation of continuing the conversation.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if no conversation reference is found for the specified conversation ID.</exception>
        public async Task ContinueConversationAsync(IChannelAdapter adapter, string conversationId, IDictionary<string, object> continueProperties, CancellationToken cancellationToken = default)
        {
            var record = await GetConversationReferenceAsync(conversationId, cancellationToken).ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"No conversation reference found for conversation ID '{conversationId}'.");

            await ContinueConversationAsync(adapter, record.Identity, record.Reference, continueProperties, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Continues an existing conversation by starting proactive turn.  <see cref="AddContinueConversationRoute(RouteHandler, string[], RouteSelector)"/>
        /// MUST have been called to register a route handler for the <c>ContinueConversation</c> Event prior to calling this method. The registered route 
        /// handler will be called with the same TurnState in the context of the original conversation, and if specified on the route the OAuth tokens.
        /// </summary>
        /// <param name="adapter">The channel adapter used to process the proactive conversation activity.</param>
        /// <param name="identity">The claims identity representing the user or bot on whose behalf the conversation is continued. Must not be
        /// null.</param>
        /// <param name="reference">The conversation reference that identifies the conversation to continue. Must not be null.</param>
        /// <param name="continueProperties">A dictionary containing properties to include in the continuation activity. These properties are passed as
        /// the activity's value.  These can be used in a custom RouteSelector to route to the desired RouteHandler.<br/><br/>The ITurnContext.Activity.Value 
        /// will contain the dictionary. The ITurnContext.Activity.ValueType will be <see cref="ContinueConversationValueType"/></param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation of continuing the conversation.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if no conversation reference is found for the specified conversation ID.</exception>
        public async Task ContinueConversationAsync(IChannelAdapter adapter, ClaimsIdentity identity, ConversationReference reference, IDictionary<string, object> continueProperties, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(adapter, nameof(adapter));
            AssertionHelpers.ThrowIfNull(identity, nameof(identity));
            AssertionHelpers.ThrowIfNull(reference, nameof(reference));

            var continuationActivity = reference.GetContinuationActivity();
            if (continueProperties?.Count > 0)
            {
                continuationActivity.ValueType = ContinueConversationValueType;
                continuationActivity.Value = continueProperties;
            }

            await adapter.ProcessProactiveAsync(identity, continuationActivity, _app, cancellationToken);
        }

        /// <summary>
        /// Registers a route handler for the ContinueConversation event, enabling the application to process conversation continuation activities.
        /// </summary>
        /// <remarks>Use this method to add custom logic for handling proactive messaging. If no selector is provided, the handler will apply to
        /// all ContinueConversation Events.</remarks>
        /// <param name="handler">The handler to execute when a ContinueConversation event is received. Cannot be null.</param>
        /// <param name="autoSignInHandlers">An optional array of handler names that require automatic sign-in before the route handler is invoked.</param>
        /// <param name="selector">An optional route selector that determines which events the handler should respond to. If null, the handler
        /// is registered for all ContinueConversation Events.</param>
        public void AddContinueConversationRoute(RouteHandler handler, string[] autoSignInHandlers = null, RouteSelector selector = null)
        {
            // TODO Proactive should fail if user not signed in if autoSignInHandlers are specified.
            //   a) Fail selector and log
            //   b) or, Report to user they need to sign in?  Somehow?

            if (selector == null)
            {
                _app.OnEvent(ActivityEventNames.ContinueConversation, handler, rank: RouteRank.Last, autoSignInHandlers: autoSignInHandlers);
            }
            else
            {
                _app.OnEvent(selector, handler, autoSignInHandlers: autoSignInHandlers);
            }
        }

        #region Conversation Storage
        /// <summary>
        /// Stores the current conversation reference in the proactive storage and returns the conversation identifier.
        /// </summary>
        /// <remarks>Use this method to enable proactive messaging scenarios by persisting conversation
        /// references. The returned conversation identifier can be used to retrieve or reference the conversation in
        /// future operations.</remarks>
        /// <param name="turnContext">The context object for the current turn, containing activity and conversation information. Cannot be null.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A string containing the conversation identifier for the stored conversation reference.</returns>
        public async Task<string> StoreConversationAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var key = GetRecordKey(turnContext.Activity.Conversation.Id);
            var record = new ConversationReferenceRecord(turnContext.Identity, turnContext.Activity.GetConversationReference());
            await _app.Options.Proactive.Storage.WriteAsync(
                new Dictionary<string, object>
                {
                    { key, record }
                },
                cancellationToken);
            return turnContext.Activity.Conversation.Id;
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
        /// <returns>A <see cref="ConversationReferenceRecord"/> representing the conversation reference if found; otherwise,
        /// <see langword="null"/>.</returns>
        public async Task<ConversationReferenceRecord?> GetConversationReferenceAsync(string conversationId, CancellationToken cancellationToken)
        {
            var key = GetRecordKey(conversationId);
            var items = await _options.Storage.ReadAsync([key], cancellationToken).ConfigureAwait(false);

            if (items != null && items.TryGetValue(key, out var item) && item is ConversationReferenceRecord record)
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
        public Task DeleteConversationReferenceAsync(string conversationId, CancellationToken cancellationToken)
        {
            var key = GetRecordKey(conversationId);
            return _options.Storage.DeleteAsync([key], cancellationToken);
        }

        private static string GetRecordKey(string conversationId)
        {
            return $"conversationreferences/{conversationId}";
        }
        #endregion
    }
}
