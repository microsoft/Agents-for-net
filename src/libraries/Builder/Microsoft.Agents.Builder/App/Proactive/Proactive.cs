// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System;
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

        public async Task ContinueConversationAsync(IChannelAdapter adapter, string conversationId, TurnEventHandler handler, CancellationToken cancellationToken)
        {
            var conversationReferenceRecord = await GetConversationReferenceAsync(conversationId, cancellationToken).ConfigureAwait(false) 
                ?? throw new InvalidOperationException($"No conversation reference found for conversation ID '{conversationId}'.");
            await ContinueConversationAsync(adapter, conversationReferenceRecord.Identity, conversationReferenceRecord.Reference, handler, cancellationToken);
        }

        public async Task ContinueConversationAsync(IChannelAdapter adapter, ClaimsIdentity identity, ConversationReference reference, TurnEventHandler handler, CancellationToken cancellationToken)
        {
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
