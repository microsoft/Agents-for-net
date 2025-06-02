// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Models
{
    public class AgentCapabilities
    {
        [JsonPropertyName("streaming")]
        public bool? Streaming { get; init; } = false;

        [JsonPropertyName("pushNotifications")]
        public bool? PushNotifications { get; init; } = false;

        [JsonPropertyName("stateTransitionHistory")]
        public bool? StateTransitionHistory { get; init; } = false;
    }
}
