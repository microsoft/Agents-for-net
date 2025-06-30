// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol
{
    /// <summary>
    /// Describes a specific capability, function, or area of expertise the agent can perform or address.
    /// </summary>
    public class AgentSkill
    {
        /// <summary>
        /// Unique skill identifier within this agent.
        /// </summary>
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        /// <summary>
        /// Human-readable skill name.
        /// </summary>
        [JsonPropertyName("name")]
        public required string Name { get; init; }

        /// <summary>
        /// Detailed skill description. CommonMark MAY be used.
        /// </summary>
        [JsonPropertyName("description")]
        public required string Description { get; init; }

        /// <summary>
        /// Keywords/categories for discoverability.
        /// </summary>
        [JsonPropertyName("tags")]
        public ImmutableArray<string> Tags { get; init; } = [];

        /// <summary>
        /// Example prompts or use cases demonstrating skill usage.
        /// </summary>
        [JsonPropertyName("examples")]
        public ImmutableArray<string>? Examples { get; init; }

        /// <summary>
        /// Overrides defaultInputModes for this specific skill. Accepted Media Types.
        /// </summary>
        [JsonPropertyName("inputModes")]
        public ImmutableArray<string>? InputModes { get; init; }

        /// <summary>
        /// Overrides defaultOutputModes for this specific skill. Produced Media Types.
        /// </summary>
        [JsonPropertyName("outputModes")]
        public ImmutableArray<string>? OutputModes { get; init; }
    }
}
