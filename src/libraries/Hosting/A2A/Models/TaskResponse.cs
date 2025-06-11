// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Models
{
    public record TaskResponse
    {
        public string Kind { get; } = "task";

        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("contextId")]
        public required string ContextId { get; init; }

        [JsonPropertyName("status")]
        public required TaskStatus Status { get; init; }

        [JsonPropertyName("artifacts")]
        public ImmutableArray<Artifact>? Artifacts { get; init; }

        [JsonPropertyName("history")]
        public ImmutableArray<Message>? History { get; init; }

        [JsonPropertyName("metadata")]
        public IReadOnlyDictionary<string, object>? Metadata { get; set; }
    }
}