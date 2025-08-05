// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol
{
    public class AgentCapabilities
    {
        /// <summary>
        /// Indicates if the agent supports Server-Sent Events (SSE) for streaming responses.
        /// </summary>
        [JsonPropertyName("streaming")]
        public bool? Streaming { get; init; } = false;

        /// <summary>
        /// Indicates if the agent supports sending push notifications for asynchronous task updates.
        /// </summary>
        [JsonPropertyName("pushNotifications")]
        public bool? PushNotifications { get; init; } = false;

        /// <summary>
        /// Indicates if the agent provides a history of state transitions for a task.
        /// </summary>
        [JsonPropertyName("stateTransitionHistory")]
        public bool? StateTransitionHistory { get; init; } = false;

        /// <summary>
        /// A list of protocol extensions supported by the agent.
        /// </summary>
        [JsonPropertyName("extensions")]
        public ImmutableArray<AgentExtension>? Extensions { get; init; }
    }
}
