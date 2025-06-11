// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Models
{
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
    [JsonDerivedType(typeof(TextPart), typeDiscriminator: "text")]
    [JsonDerivedType(typeof(FilePart), typeDiscriminator: "file")]
    [JsonDerivedType(typeof(DataPart), typeDiscriminator: "data")]
    public abstract record Part
    {
        [JsonPropertyName("metadata")]
        public IReadOnlyDictionary<string, object>? Metadata { get; set; }
    }

    public record TextPart : Part
    {
        [JsonPropertyName("text")]
        public required string Text { get; init; }
    }

    public record FilePart : Part
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("mimeType")]
        public string? MimeType { get; init; }

        [JsonPropertyName("bytes")]
        public string? Bytes { get; init; }

        [JsonPropertyName("uri")]
        public string? Uri { get; init; }
    }

    public record DataPart : Part
    {
        [JsonPropertyName("data")]
        public required object Data { get; init; }
    }
}