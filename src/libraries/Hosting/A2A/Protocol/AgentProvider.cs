// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol;

/// <summary>
/// Information about the organization or entity providing the agent.
/// </summary>
public record AgentProvider
{
    /// <summary>
    /// Name of the organization/entity.
    /// </summary>
    [JsonPropertyName("organization")]
    public required string Organization { get; init; }

    /// <summary>
    /// URL for the provider's website/contact.
    /// </summary>
    [JsonPropertyName("url")]
    public required string Url { get; init; }
}
