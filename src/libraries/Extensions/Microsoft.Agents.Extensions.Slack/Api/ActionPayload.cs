// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Extensions.Slack.Api
{
    public class ActionPayload : SlackModel
    {
        public string type { get; set; }

        /// <summary>
        /// The channel the interaction occurred in. Slack sends this either as a bare id string
        /// (legacy <c>interactive_message</c> payloads) or as an object (<c>block_actions</c> and
        /// newer payloads); in both cases this exposes the channel id.
        /// </summary>
        [JsonConverter(typeof(ChannelIdConverter))]
        public string channel { get; set; }

        public object message { get; set; }
        public object actions { get; set; }

        /// <summary>Catch-all for any envelope fields not explicitly modelled above.</summary>
        [JsonExtensionData]
        public IDictionary<string, JsonElement> AdditionalProperties { get; set; } = new Dictionary<string, JsonElement>();
    }

    /// <summary>
    /// Reads the Slack interactivity <c>channel</c> field, which may be a bare id string or an
    /// object of the form <c>{ "id": "C123", "name": "general" }</c>, and yields the channel id.
    /// </summary>
    internal sealed class ChannelIdConverter : System.Text.Json.Serialization.JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString();
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                using var document = JsonDocument.ParseValue(ref reader);
                return document.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
            }

            reader.Skip();
            return null;
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStringValue(value);
            }
        }
    }
}
