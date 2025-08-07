// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol;

/// <summary>
/// For conveying plain textual content.
/// </summary>
public record TextPart : Part
{
    /// <summary>
    /// The textual content of the part.
    /// </summary>
    [JsonPropertyName("text")]
    public required string Text { get; init; }
}
