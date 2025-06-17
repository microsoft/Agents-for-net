// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol
{
    /// <summary>
    /// Specifies an extension to the A2A protocol supported by the agent.
    /// </summary>
    public record AgentExtension
    {
        /// <summary>
        /// The URI for the supported extension.
        /// </summary>
        [JsonPropertyName("url")]
        public required string Url { get; init; }

        /// <summary>
        /// Whether the agent requires clients to follow some protocol logic specific to the extension. 
        /// Clients should expect failures when attempting to interact with a server that requires an 
        /// extension the client does not support.
        /// </summary>
        [JsonPropertyName("required")]
        public bool? Required { get; init; } = false;

        /// <summary>
        /// A description of how the extension is used by the agent.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; init; }

        /// <summary>
        /// Configuration parameters specific to the extension
        /// </summary>
        [JsonPropertyName("params")]
        public object? Params { get; init; }
    }
}
