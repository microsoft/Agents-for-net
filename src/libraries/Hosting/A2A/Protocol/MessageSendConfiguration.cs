// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol
{
    public record MessageSendConfiguration
    {
        [JsonPropertyName("acceptedOutputModes")]
        public ImmutableArray<string>? AcceptedOutputModes { get; init; }

        [JsonPropertyName("blocking")]
        public bool? Blocking { get; init; }

        [JsonPropertyName("historyLength")]
        public int? HistoryLength { get; init; }

        //PushNotificationConfig? pushNotificationConfig
    }
}