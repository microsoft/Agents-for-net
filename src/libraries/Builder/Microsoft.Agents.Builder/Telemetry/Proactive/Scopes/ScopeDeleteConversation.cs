// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Telemetry;
using System;

namespace Microsoft.Agents.Builder.Telemetry.Proactive.Scopes
{
    /// <summary>
    /// A <see cref="TelemetryScope"/> that traces the deletion of a stored proactive
    /// conversation reference.
    /// </summary>
    /// <remarks>
    /// Records the conversation identifier being removed as a span tag.
    /// </remarks>
    internal class ScopeDeleteConversation : TelemetryScope
    {

        private readonly string _conversationId;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeDeleteConversation"/> class.
        /// </summary>
        /// <param name="conversationId">The identifier of the stored conversation to delete.</param>
        public ScopeDeleteConversation(string conversationId) : base(Constants.ScopeDeleteConversation)
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