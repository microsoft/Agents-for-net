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
        /// <param name="conversationId">The unique identifier of the conversation to which the activity will be sent. Cannot be null or empty.</param>
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
        public static async Task<ResourceResponse> SendActivityAsync(IChannelAdapter adapter, ConversationReferenceRecord record, IActivity activity, CancellationToken cancellationToken)
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
        /// ID.
        /// </summary>
        /// <param name="adapter">The channel adapter used to send and receive activities for the conversation.</param>
        /// <param name="conversationId">The unique identifier of the conversation to continue. Cannot be null or empty.</param>
        /// <param name="handler">A delegate that handles the routing of activities within the continued conversation.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if no conversation reference is found for the specified conversation ID.</exception>
        public async Task ContinueConversationAsync(IChannelAdapter adapter, string conversationId, RouteHandler handler, CancellationToken cancellationToken)
        {
            var record = await GetConversationReferenceAsync(conversationId, cancellationToken).ConfigureAwait(false) 
                ?? throw new KeyNotFoundException($"No conversation reference found for conversation ID '{conversationId}'.");
            await ContinueConversationAsync(adapter, record.Identity, record.Reference, handler, cancellationToken);
        }

        /// <summary>
        /// Continues an existing conversation by invoking the specified route handler within the context of the
        /// provided conversation reference.
        /// </summary>
        /// <remarks>This method loads and saves turn state before and after invoking the route handler.
        /// It is typically used to process additional activities or operations in the context of an existing
        /// conversation, such as proactive messaging or background event handling.</remarks>
        /// <param name="adapter">The channel adapter used to continue the conversation. Must not be null.</param>
        /// <param name="identity">The claims identity representing the user or bot on whose behalf the conversation is continued. Must not be
        /// null.</param>
        /// <param name="reference">The conversation reference that identifies the conversation to continue. Must not be null.</param>
        /// <param name="handler">The route handler delegate to execute within the continued conversation context. Must not be null.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task ContinueConversationAsync(IChannelAdapter adapter, ClaimsIdentity identity, ConversationReference reference, RouteHandler handler, CancellationToken cancellationToken)
        {
            AssertionHelpers.ThrowIfNull(adapter, nameof(adapter));
            AssertionHelpers.ThrowIfNull(identity, nameof(identity));
            AssertionHelpers.ThrowIfNull(reference, nameof(reference));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            await adapter.ContinueConversationAsync(identity, reference, async (turnContext, ct) =>
            {
                var turnState = _app.Options.TurnStateFactory!();
                await turnState.LoadStateAsync(turnContext, false, ct).ConfigureAwait(false);

                // OAuth?

                await handler(turnContext, turnState, ct).ConfigureAwait(false);

                await turnState.SaveStateAsync(turnContext, false, ct).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }

        public Task StoreConversationAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var key = ConversationReferenceRecord.GetKey(turnContext.Activity.Conversation.Id);
            var record = new ConversationReferenceRecord(turnContext.Identity, turnContext.Activity.GetConversationReference());
            return _app.Options.Proactive.Storage.WriteAsync(
                new Dictionary<string, object>
                {
                    { key, record }
                },
                cancellationToken);
        }

        public async Task<ConversationReferenceRecord?> GetConversationReferenceAsync(string conversationId, CancellationToken cancellationToken)
        {
            var key = ConversationReferenceRecord.GetKey(conversationId);
            var items = await _options.Storage.ReadAsync([key], cancellationToken).ConfigureAwait(false);

            if (items != null && items.TryGetValue(key, out var item) && item is ConversationReferenceRecord record)
            {
                return record;
            }
            return null;
        }
    }
}
