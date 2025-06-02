// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Models
{
    public record TaskStatus
    {
        [JsonPropertyName("state")]
        public required string State { get; init; }

        [JsonPropertyName("message")]
        public Message? Message { get; init; }

        [JsonPropertyName("timestamp")]
        public DateTimeOffset? Timestamp { get; init; }
    }
}