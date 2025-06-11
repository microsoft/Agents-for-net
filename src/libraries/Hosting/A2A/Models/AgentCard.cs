// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Models
{
    /// <summary>
    /// A2A Servers MUST make an Agent Card available. The Agent Card is a JSON document that describes the 
    /// server's identity, capabilities, skills, service endpoint URL, and how clients should authenticate 
    /// and interact with it. Clients use this information for discovering suitable agents and for configuring 
    /// their interactions.
    /// </summary>
    public class AgentCard
    {
        /// <summary>
        /// Human-readable name of the agent.
        /// </summary>
        [JsonPropertyName("name")]
        public required string Name { get; init; }

        /// <summary>
        /// Human-readable description. CommonMark MAY be used.
        /// </summary>
        [JsonPropertyName("description")]
        public required string Description { get; init; }

        /// <summary>
        /// Base URL for the agent's A2A service. Must be absolute. HTTPS for production.
        /// </summary>
        [JsonPropertyName("url")]
        public required string Url { get; init; }

        /// <summary>
        /// Information about the agent's provider.
        /// </summary>
        public AgentProvider? Provider { get; init; }

        /// <summary>
        /// Agent or A2A implementation version string.
        /// </summary>
        [JsonPropertyName("version")]
        public required string Version { get; init; }

        /// <summary>
        /// URL to human-readable documentation for the agent.
        /// </summary>
        [JsonPropertyName("documentationUrl")]
        public string? DocumentationUrl { get; init; }

        /// <summary>
        /// Specifies optional A2A protocol features supported (e.g., streaming, push notifications).
        /// </summary>
        [JsonPropertyName("capabilities")]
        public required AgentCapabilities Capabilities { get; init; } = new AgentCapabilities();

        //security

        /// <summary>
        /// Security scheme details used for authenticating with this agent. Undefined implies no 
        /// A2A-advertised auth (not recommended for production).
        /// </summary>
        public IReadOnlyDictionary<string, SecurityScheme>? SecuritySchemes { get; init;}

        /// <summary>
        /// Input Media Types accepted by the agent.
        /// </summary>
        [JsonPropertyName("defaultInputModes")]
        public required ImmutableArray<string> DefaultInputModes { get; init; }

        /// <summary>
        /// Output Media Types produced by the agent.
        /// </summary>
        [JsonPropertyName("defaultOutputModes")]
        public required ImmutableArray<string> DefaultOutputModes { get; init; }

        /// <summary>
        /// Array of skills. Must have at least one if the agent performs actions.
        /// </summary>
        [JsonPropertyName("skills")]
        public required ImmutableArray<AgentSkill> Skills { get; init; }

        /// <summary>
        /// Indicates support for retrieving a more detailed Agent Card via an authenticated endpoint.
        /// </summary>
        [JsonPropertyName("supportsAuthenticatedExtendedCard")]
        public string? SupportsAuthenticatedExtendedCard { get; init; }
    }
}