// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Models
{
    public record Message
    {
        [JsonPropertyName("kind")]
        public string Kind { get; } = "message";

        /// <summary>
        /// 
        /// </summary>
        [JsonPropertyName("role")]
        public required string Role { get; init; }

        /// <summary>
        /// 
        /// </summary>
        [JsonPropertyName("parts")]
        public required ImmutableArray<Part> Parts { get; init; }

        /// <summary>
        /// 
        /// </summary>
        [JsonPropertyName("messageId")]
        public required string MessageId { get; init; }

        /// <summary>
        /// 
        /// </summary>
        [JsonPropertyName("taskId")]
        public string? TaskId { get; init; }

        /// <summary>
        /// 
        /// </summary>
        [JsonPropertyName("contextId")]
        public string? ContextId { get; init; }

        /// <summary>
        /// 
        /// </summary>
        [JsonPropertyName("metadata")]
        public IReadOnlyDictionary<string, object>? Metadata { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [JsonPropertyName("referenceTaskIds")]
        public ImmutableArray<string>? ReferenceTaskIds { get; init; }
    }
}