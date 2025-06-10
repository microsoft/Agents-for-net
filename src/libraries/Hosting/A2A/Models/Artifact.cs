// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Models
{
    public record Artifact
    {
        public static readonly Artifact Empty = new() { ArtifactId = Guid.NewGuid().ToString("N") };

        [JsonPropertyName("artifactId")]
        public required string ArtifactId { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("parts")]
        public ImmutableArray<Part> Parts { get; init; } = [];
    }

}