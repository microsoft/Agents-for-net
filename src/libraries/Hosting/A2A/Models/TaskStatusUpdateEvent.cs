// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Models
{
    public record TaskStatusUpdateEvent
    {
        public string Kind { get; } = "status-update";

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

        [JsonPropertyName("status")]
        public required TaskStatus Status { get; init; }

        [JsonPropertyName("final")]
        public bool? Final { get; init; }

        [JsonPropertyName("metadata")]
        public IDictionary<string, object>? Metadata { get; set; }
    }
}