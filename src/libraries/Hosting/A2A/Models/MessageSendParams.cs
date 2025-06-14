// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Models
{
    public record MessageSendParams
    {
        [JsonPropertyName("message")]
        public required Message Message { get; init; }

        [JsonPropertyName("configuration")]
        public MessageSendConfiguration? Configuration { get; init; }

        [JsonPropertyName("metadata")]
        public IReadOnlyDictionary<string, object>? Metadata { get; set; }
    }
}