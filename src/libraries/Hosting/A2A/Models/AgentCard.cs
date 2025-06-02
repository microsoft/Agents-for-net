// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Models
{
    public class AgentCard
    {
        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("description")]
        public required string Description { get; init; }

        [JsonPropertyName("url")]
        public required string Url { get; init; }

        //provider

        [JsonPropertyName("version")]
        public required string Version { get; init; }

        [JsonPropertyName("documentationUrl")]
        public string? DocumentationUrl { get; init; }

        [JsonPropertyName("capabilities")]
        public required AgentCapabilities Capabilities { get; init; } = new AgentCapabilities();

        //securitySchemes
        //security

        [JsonPropertyName("defaultInputModes")]
        public required ImmutableArray<string> DefaultInputModes { get; init; }

        [JsonPropertyName("defaultOutputModes")]
        public required ImmutableArray<string> DefaultOutputModes { get; init; }

        [JsonPropertyName("skills")]
        public required ImmutableArray<AgentSkill> Skills { get; init; }

        [JsonPropertyName("supportsAuthenticatedExtendedCard")]
        public string? SupportsAuthenticatedExtendedCard { get; init; }
    }
}