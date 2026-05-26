// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Builder.Telemetry.Proactive
{
    /// <summary>
    /// Defines the span (activity) names used by the proactive-message telemetry scopes.
    /// </summary>
    internal static class Constants
    {
        /// <summary>Span name for storing a conversation reference for future proactive use.</summary>
        internal static readonly string ScopeStoreConversation = "agents.proactive.store_conversation";

        /// <summary>Span name for retrieving a stored conversation reference.</summary>
        internal static readonly string ScopeGetConversation = "agents.proactive.get_conversation";

        /// <summary>Span name for deleting a stored conversation reference.</summary>
        internal static readonly string ScopeDeleteConversation = "agents.proactive.delete_conversation";

        /// <summary>Span name for sending an activity proactively to an existing conversation.</summary>
        internal static readonly string ScopeSendActivity = "agents.proactive.send_activity";

        /// <summary>Span name for continuing a stored conversation proactively.</summary>
        internal static readonly string ScopeContinueConversation = "agents.proactive.continue_conversation";

        /// <summary>Span name for creating a new proactive conversation.</summary>
        internal static readonly string ScopeCreateConversation = "agents.proactive.create_conversation";
    }
}
