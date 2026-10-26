// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Extensions.Slack.Api
{
    public class ActionPayload : SlackModel
    {
        public string type { get; set; }

        [JsonConverter(typeof(SlackChannelIdConverter))]
        public string channel { get; set; }
        public object message { get; set; }
        public object actions { get; set; }

        /// <summary>Catch-all for any envelope fields not explicitly modelled above.</summary>
        [JsonExtensionData]
        public IDictionary<string, JsonElement> AdditionalProperties { get; set; } = new Dictionary<string, JsonElement>();
    }

    internal sealed class SlackChannelIdConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString();
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Slack channel must be a string or an object.");
            }

            using var channel = JsonDocument.ParseValue(ref reader);
            if (!channel.RootElement.TryGetProperty("id", out var channelId) || channelId.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (channelId.ValueKind != JsonValueKind.String)
            {
                throw new JsonException("Slack channel id must be a string.");
            }

            return channelId.GetString();
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
            => writer.WriteStringValue(value);
    }
}
