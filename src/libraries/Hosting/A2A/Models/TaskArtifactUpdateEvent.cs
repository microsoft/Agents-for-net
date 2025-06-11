// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Models
{
    public record TaskArtifactUpdateEvent
    {
        public const string TaskArtifactUpdateEventKind = "artifact-update";

        public string Kind { get; } = TaskArtifactUpdateEventKind;

        /// <summary>
        /// Task ID being updated
        /// </summary>
        [JsonPropertyName("taskId")]
        public required string TaskId { get; init; }

        /// <summary>
        /// Context ID the task is associated with
        /// </summary>
        [JsonPropertyName("contextId")]
        public required string ContextId { get; init; }

        [JsonPropertyName("artifact")]
        public required Artifact Artifact { get; init; }

        [JsonPropertyName("append")]
        public bool? Append { get; init; }

        [JsonPropertyName("lastChunk")]
        public bool? LastChunk { get; init; }

        [JsonPropertyName("metadata")]
        public IDictionary<string, object>? Metadata { get; set; }
    }
}