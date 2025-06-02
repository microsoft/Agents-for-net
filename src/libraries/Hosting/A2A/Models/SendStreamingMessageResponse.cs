// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Models
{
    public record SendStreamingMessageResponse
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; } = "2.0";

        /// <summary>
        /// Matches the id from the originating tasks/sendSubscribe or tasks/resubscribe TaskSendParams.
        /// </summary>
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        /// <summary>
        /// TaskArtifactUpdateEvent or TaskStatusUpdateEvent
        /// </summary>
        [JsonPropertyName("result")]
        public required object Result { get; init; }
    }
}