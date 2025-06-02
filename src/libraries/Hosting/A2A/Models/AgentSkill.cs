// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Models
{
    public class AgentSkill
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("description")]
        public required string Description { get; init; }

        [JsonPropertyName("tags")]
        public ImmutableArray<string> Tags { get; init; } = [];

        [JsonPropertyName("examples")]
        public ImmutableArray<string>? Examples { get; init; }

        [JsonPropertyName("inputModes")]
        public ImmutableArray<string>? InputModes { get; init; }

        [JsonPropertyName("outputModes")]
        public ImmutableArray<string>? OutputModes { get; init; }
    }
}
