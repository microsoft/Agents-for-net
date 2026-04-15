// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Extensions.Slack.Api
{
    public class SlackEvent
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("user")]
        public string User { get; set; }

        [JsonPropertyName("ts")]
        public string Ts { get; set; }

        [JsonPropertyName("client_msg_id")]
        public string ClientMsgId { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("team")]
        public string Team { get; set; } = string.Empty;

        [JsonPropertyName("channel")]
        public string Channel { get; set; } = string.Empty;

        [JsonPropertyName("event_ts")]
        public string EventTs { get; set; } = string.Empty;

        [JsonPropertyName("channel_type")]
        public string ChannelType { get; set; } = string.Empty;

        [JsonExtensionData]
        public IDictionary<string, JsonElement> Properties { get; set; } = new Dictionary<string, JsonElement>();
    }
}
