// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol;

/// <summary>
/// For conveying file-based content.
/// </summary>
public record FilePart : Part
{
    /// <summary>
    /// Original filename (e.g., "report.pdf").
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// Media Type (e.g., image/png). Strongly recommended.
    /// </summary>
    [JsonPropertyName("mimeType")]
    public string? MimeType { get; init; }

    /// <summary>
    /// Base64 encoded file content.
    /// </summary>
    [JsonPropertyName("bytes")]
    public string? Bytes { get; init; }

    /// <summary>
    /// URI (absolute URL strongly recommended) to file content. Accessibility is context-dependent.
    /// </summary>
    [JsonPropertyName("uri")]
    public string? Uri { get; init; }
}
