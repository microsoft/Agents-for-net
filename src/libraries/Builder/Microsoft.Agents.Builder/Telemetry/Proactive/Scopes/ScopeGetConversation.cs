// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Telemetry;
using System;

namespace Microsoft.Agents.Builder.Telemetry.Proactive.Scopes
{
    /// <summary>
    /// A <see cref="TelemetryScope"/> that traces retrieval of a stored proactive
    /// conversation reference.
    /// </summary>
    /// <remarks>
    /// Records the requested conversation identifier and, when shared, whether the
    /// conversation was found.
    /// </remarks>
    internal class ScopeGetConversation : TelemetryScope
    {

        private readonly string _conversationId;
        private bool? _conversationFound;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeGetConversation"/> class.
        /// </summary>
        /// <param name="conversationId">The identifier of the conversation being retrieved.</param>
        public ScopeGetConversation(string conversationId) : base(Constants.ScopeGetConversation)
        {
            _conversationId = conversationId;
        }

        /// <inheritdoc />
        protected override void Callback(System.Diagnostics.Activity activity, double duration, Exception? exception)
        {
            activity.SetTag(TagNames.ConversationId, _conversationId);
            activity.SetTag(TagNames.ConversationFound, _conversationFound);
        }

        /// <summary>
        /// Shares whether the requested conversation was found so the value can be emitted
        /// as telemetry when the scope completes.
        /// </summary>
        /// <param name="conversationFound"><see langword="true"/> if the conversation was found; otherwise, <see langword="false"/>.</param>
        public void Share(bool conversationFound)
        {
            _conversationFound = conversationFound;
        }
    }
}