// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
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

        public Task SendActivityAsync(IChannelAdapter adapter, string conversationId, IActivity activity, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(activity.Type))
            {
                activity.Type = ActivityTypes.Message;
            }

            return ContinueConversationAsync(adapter, conversationId, (turnContext, turnState, ct) =>
            {
                return turnContext.SendActivityAsync(activity, ct);
            }, cancellationToken);
        }

        public Task SendActivityAsync(IChannelAdapter adapter, ConversationReferenceRecord record, IActivity activity, CancellationToken cancellationToken)
        {
            return ContinueConversationAsync(adapter, record.Identity, record.Reference, (turnContext, turnState, ct) =>
            {
                return turnContext.SendActivityAsync(activity, ct);
            }, cancellationToken);
        }

        public async Task ContinueConversationAsync(IChannelAdapter adapter, string conversationId, RouteHandler handler, CancellationToken cancellationToken)
        {
            var conversationReferenceRecord = await GetConversationReferenceAsync(conversationId, cancellationToken).ConfigureAwait(false) 
                ?? throw new KeyNotFoundException($"No conversation reference found for conversation ID '{conversationId}'.");
            await ContinueConversationAsync(adapter, conversationReferenceRecord.Identity, conversationReferenceRecord.Reference, handler, cancellationToken);
        }

        public async Task ContinueConversationAsync(IChannelAdapter adapter, ClaimsIdentity identity, ConversationReference reference, RouteHandler handler, CancellationToken cancellationToken)
        {
            AssertionHelpers.ThrowIfNull(adapter, nameof(adapter));
            AssertionHelpers.ThrowIfNull(identity, nameof(identity));
            AssertionHelpers.ThrowIfNull(reference, nameof(reference));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            await adapter.ContinueConversationAsync(identity, reference, async (turnContext, ct) =>
            {
                // TODO SendActivityAsync doesn't need to load state
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
            var json = ProtocolJsonSerializer.ToJson(record);
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
