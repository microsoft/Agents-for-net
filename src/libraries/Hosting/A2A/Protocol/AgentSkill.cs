// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol
{
    /// <summary>
    /// Represents a distinct capability or function that an agent can perform.
    /// </summary>
    public class AgentSkill
    {
        /// <summary>
        /// A unique identifier for the agent's skill.
        /// </summary>
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        /// <summary>
        /// A human-readable name for the skill.
        /// </summary>
        [JsonPropertyName("name")]
        public required string Name { get; init; }

        /// <summary>
        /// A detailed description of the skill, intended to help clients or users understand its purpose and functionality.
        /// </summary>
        [JsonPropertyName("description")]
        public required string Description { get; init; }

        /// <summary>
        /// A set of keywords describing the skill's capabilities.
        /// </summary>
        [JsonPropertyName("tags")]
        public ImmutableArray<string> Tags { get; init; } = [];

        /// <summary>
        /// Example prompts or scenarios that this skill can handle. Provides a hint to the client on how to use the skill.
        /// </summary>
        [JsonPropertyName("examples")]
        public ImmutableArray<string>? Examples { get; init; }

        /// <summary>
        /// The set of supported input MIME types for this skill, overriding the agent's defaults.
        /// </summary>
        [JsonPropertyName("inputModes")]
        public ImmutableArray<string>? InputModes { get; init; }

        /// <summary>
        /// The set of supported output MIME types for this skill, overriding the agent's defaults.
        /// </summary>
        [JsonPropertyName("outputModes")]
        public ImmutableArray<string>? OutputModes { get; init; }

        /// <summary>
        /// Security schemes necessary for the agent to leverage this skill. As in the overall AgentCard.security, this list represents a logical OR of security
        /// requirement objects. Each object is a set of security schemes that must be used together (a logical AND).
        /// </summary>
        [JsonPropertyName("security")]
        public IReadOnlyDictionary<string, ImmutableArray<string>>? Security { get; init; }
    }
}
