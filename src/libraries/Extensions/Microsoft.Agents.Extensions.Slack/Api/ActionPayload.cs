// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Extensions.Slack.Api
{
    [JsonConverter(typeof(ActionPayloadConverter))]
    public class ActionPayload : SlackModel
    {
        internal readonly JsonObject _data;

        public ActionPayload()
        {
        }

        internal ActionPayload(JsonObject data)
        {
            _data = data;
        }

        protected override JsonObject GetData() => _data ?? ActionPayloadConverter.CreateJsonObject(this, new JsonSerializerOptions());

        public string type { get; set; }

        /// <summary>
        /// The channel the interaction occurred in. Slack sends this either as a bare id string
        /// (legacy <c>interactive_message</c> payloads) or as an object (<c>block_actions</c> and
        /// newer payloads); in both cases this exposes the channel id.
        /// </summary>
        public string channel { get; set; }

        public object message { get; set; }
        public object actions { get; set; }

        /// <summary>Catch-all for any envelope fields not explicitly modelled above.</summary>
        [JsonExtensionData]
        public IDictionary<string, JsonElement> AdditionalProperties { get; set; } = new Dictionary<string, JsonElement>();
    }

    /// <summary>
    /// Preserves the complete Slack interactivity payload while populating the public typed
    /// properties used by existing callers.
    /// </summary>
    internal sealed class ActionPayloadConverter : JsonConverter<ActionPayload>
    {
        public override ActionPayload Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        {
            var data = JsonSerializer.Deserialize<JsonObject>(ref reader, options) ?? new JsonObject();
            var payload = new ActionPayload(data)
            {
                type = GetString(GetProperty(data, "type", options)),
                channel = GetChannelId(GetProperty(data, "channel", options)),
                message = GetProperty(data, "message", options)?.Deserialize<object>(options),
                actions = GetProperty(data, "actions", options)?.Deserialize<object>(options),
            };

            foreach (var property in data)
            {
                if (!IsKnownProperty(property.Key, options))
                {
                    payload.AdditionalProperties[property.Key] =
                        JsonSerializer.SerializeToElement(property.Value, options);
                }
            }

            return payload;
        }

        public override void Write(Utf8JsonWriter writer, ActionPayload value, JsonSerializerOptions options)
        {
            if (value?._data != null)
            {
                value._data.WriteTo(writer, options);
            }
            else
            {
                CreateJsonObject(value, options).WriteTo(writer, options);
            }
        }

        internal static JsonObject CreateJsonObject(ActionPayload value, JsonSerializerOptions options)
        {
            var data = new JsonObject();
            AddProperty(data, "type", value?.type, options);
            AddProperty(data, "channel", value?.channel, options);
            AddProperty(data, "message", value?.message, options);
            AddProperty(data, "actions", value?.actions, options);

            if (value?.AdditionalProperties != null)
            {
                foreach (var property in value.AdditionalProperties)
                {
                    if (!IsKnownProperty(property.Key, options))
                    {
                        data[property.Key] = JsonNode.Parse(property.Value.GetRawText());
                    }
                }
            }

            return data;
        }

        private static void AddProperty(JsonObject data, string name, object value, JsonSerializerOptions options)
        {
            if (value != null || options.DefaultIgnoreCondition == JsonIgnoreCondition.Never)
            {
                data[name] = JsonSerializer.SerializeToNode(value, options);
            }
        }

        private static bool IsKnownProperty(string name, JsonSerializerOptions options)
            => IsProperty(name, "type", options)
                || IsProperty(name, "channel", options)
                || IsProperty(name, "message", options)
                || IsProperty(name, "actions", options);

        private static bool IsProperty(string actual, string expected, JsonSerializerOptions options)
            => string.Equals(
                actual,
                expected,
                options.PropertyNameCaseInsensitive
                    ? System.StringComparison.OrdinalIgnoreCase
                    : System.StringComparison.Ordinal);

        private static JsonNode GetProperty(JsonObject data, string name, JsonSerializerOptions options)
        {
            foreach (var property in data)
            {
                if (IsProperty(property.Key, name, options))
                {
                    return property.Value;
                }
            }

            return null;
        }

        private static string GetChannelId(JsonNode channel)
            => channel switch
            {
                JsonValue value when value.TryGetValue<string>(out var id) => id,
                JsonObject channelObject => GetString(channelObject["id"]),
                _ => null,
            };

        private static string GetString(JsonNode value)
            => value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var result)
                ? result
                : null;
    }
}
