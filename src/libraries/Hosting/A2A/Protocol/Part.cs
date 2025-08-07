// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol;

/// <summary>
/// Represents a distinct piece of content within a Message or Artifact. A Part is a union type 
/// representing exportable content as either TextPart, FilePart, or DataPart. All Part types 
/// also include an optional metadata field for part-specific metadata.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(TextPart), typeDiscriminator: "text")]
[JsonDerivedType(typeof(FilePart), typeDiscriminator: "file")]
[JsonDerivedType(typeof(DataPart), typeDiscriminator: "data")]
public abstract record Part
{
    /// <summary>
    /// Optional metadata specific to this text part.
    /// </summary>
    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, object>? Metadata { get; set; }
}

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

/// <summary>
/// For conveying structured JSON data. Useful for forms, parameters, or any machine-readable information.
/// </summary>
public record DataPart : Part
{
    /// <summary>
    /// The structured JSON data payload (an object or an array).
    /// </summary>
    [JsonPropertyName("data")]
    public required object Data { get; init; }
}