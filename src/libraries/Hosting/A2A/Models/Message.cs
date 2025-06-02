// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Models
{
    public record Message
    {
        public string Kind { get; } = "message";

        [JsonPropertyName("role")]
        public required string Role { get; init; }

        [JsonPropertyName("parts")]
        public required ImmutableArray<Part> Parts { get; init; }

        [JsonPropertyName("messageId")]
        public string? MessageId { get; init; }

        [JsonPropertyName("taskId")]
        public string? TaskId { get; init; }

        [JsonPropertyName("contextId")]
        public string? ContextId { get; init; }

        //metadata
        //referenceTaskIds
    }
}