// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Telemetry;
using System;
using System.Diagnostics;

namespace Microsoft.Agents.Builder.Telemetry.Proactive.Scopes
{
    /// <summary>
    /// A <see cref="TelemetryScope"/> that traces storage of a conversation reference for
    /// later proactive use.
    /// </summary>
    /// <remarks>
    /// Records the conversation identifier being stored as a span tag.
    /// </remarks>
    internal class ScopeStoreConversation : TelemetryScope
    {

        private readonly string _conversationId;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeStoreConversation"/> class.
        /// </summary>
        /// <param name="conversationId">The identifier of the conversation being stored.</param>
        public ScopeStoreConversation(string conversationId) : base(Constants.ScopeStoreConversation)
        {
            _conversationId = conversationId;
        }

        /// <inheritdoc />
        protected override void Callback(System.Diagnostics.Activity activity, double duration, Exception? exception)
        {
            activity.SetTag(TagNames.ConversationId, _conversationId);
        }
    }
}