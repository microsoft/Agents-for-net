// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Models
{
    public class AgentCapabilities
    {
        /// <summary>
        /// Indicates support for SSE streaming methods (message/stream, tasks/resubscribe).
        /// </summary>
        [JsonPropertyName("streaming")]
        public bool? Streaming { get; init; } = false;

        /// <summary>
        /// Indicates support for push notification methods (tasks/pushNotificationConfig/*).
        /// </summary>
        [JsonPropertyName("pushNotifications")]
        public bool? PushNotifications { get; init; } = false;

        /// <summary>
        /// Placeholder for future feature: exposing detailed task status change history.
        /// </summary>
        [JsonPropertyName("stateTransitionHistory")]
        public bool? StateTransitionHistory { get; init; } = false;

        /// <summary>
        /// A list of extensions supported by this agent.
        /// </summary>
        [JsonPropertyName("extensions")]
        public ImmutableArray<AgentExtension>? Extensions { get; init; }
    }
}
