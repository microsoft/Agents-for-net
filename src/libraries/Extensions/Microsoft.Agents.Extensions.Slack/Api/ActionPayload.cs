// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Agents.Builder.State;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Extensions.Slack.Api
{
    [JsonConverter(typeof(ActionPayloadConverter))]
    public sealed class ActionPayload : SlackModel
    {
        private readonly JsonObject _data;

        internal ActionPayload(
            JsonObject data,
            string type,
            string channel,
            object message,
            object actions,
            IDictionary<string, JsonElement> additionalProperties)
        {
            _data = data;
            this.type = type;
            this.channel = channel;
            this.message = message;
            this.actions = actions;
            AdditionalProperties = new ReadOnlyDictionary<string, JsonElement>(
                new Dictionary<string, JsonElement>(additionalProperties));
        }

        protected override JsonObject GetData() => _data.DeepClone().AsObject();

        public override T Get<T>(string path)
        {
            TryGet(path, out T value);
            return value;
        }

        public override bool TryGet<T>(string path, out T value)
        {
            var normalizedPath = string.IsNullOrEmpty(path) ? string.Empty : NormalizePath(path);
            if (!ObjectPath.TryGetPathValue(_data, normalizedPath, out value))
            {
                return false;
            }

            value = DetachMutableJson(value);
            return true;
        }

        internal void WriteNormalizedRaw(Utf8JsonWriter writer, JsonSerializerOptions options)
            => _data.WriteTo(writer, options);

        private static T DetachMutableJson<T>(T value)
        {
            if (value is JsonObject jsonObject)
            {
                return (T)(object)jsonObject.DeepClone();
            }

            if (value is JsonArray jsonArray)
            {
                return (T)(object)jsonArray.DeepClone();
            }

            return value;
        }

        public string type { get; }

        /// <summary>
        /// The channel the interaction occurred in. Slack sends this either as a bare id string
        /// (legacy <c>interactive_message</c> payloads) or as an object (<c>block_actions</c> and
        /// newer payloads); in both cases this exposes the channel id.
        /// </summary>
        public string channel { get; }

        public object message { get; }
        public object actions { get; }

        /// <summary>Catch-all for any envelope fields not explicitly modelled above.</summary>
        public IReadOnlyDictionary<string, JsonElement> AdditionalProperties { get; }
    }

    /// <summary>
    /// Preserves the complete Slack interactivity payload while populating the public typed
    /// properties used by existing callers.
    /// </summary>
    internal sealed class ActionPayloadConverter : JsonConverter<ActionPayload>
    {
        public override ActionPayload Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var data = document.RootElement.ValueKind switch
            {
                JsonValueKind.Object => CreateNormalizedObject(document.RootElement, options, normalizeKnownProperties: true),
                JsonValueKind.Null => new JsonObject(CreateNodeOptions(options)),
                _ => throw new JsonException("The Slack action payload must be a JSON object."),
            };

            var additionalProperties = new Dictionary<string, JsonElement>();
            foreach (var property in data)
            {
                if (!IsKnownProperty(property.Key, options))
                {
                    additionalProperties[property.Key] =
                        JsonSerializer.SerializeToElement(property.Value, options);
                }
            }

            return new ActionPayload(
                data,
                GetString(GetProperty(data, "type", options)),
                GetChannelId(GetProperty(data, "channel", options)),
                GetElement(GetProperty(data, "message", options), options),
                GetElement(GetProperty(data, "actions", options), options),
                additionalProperties);
        }

        private static JsonObject CreateNormalizedObject(
            JsonElement element,
            JsonSerializerOptions options,
            bool normalizeKnownProperties = false)
        {
            var result = new JsonObject(CreateNodeOptions(options));

            foreach (var property in element.EnumerateObject())
            {
                var propertyName = normalizeKnownProperties
                    ? GetKnownPropertyName(property.Name, options) ?? property.Name
                    : property.Name;
                result.Remove(propertyName);
                result.Add(propertyName, CreateNormalizedNode(property.Value, options));
            }

            return result;
        }

        private static JsonArray CreateNormalizedArray(JsonElement element, JsonSerializerOptions options)
        {
            var result = new JsonArray(CreateNodeOptions(options));

            foreach (var item in element.EnumerateArray())
            {
                result.Add(CreateNormalizedNode(item, options));
            }

            return result;
        }

        private static JsonNode CreateNormalizedNode(JsonElement element, JsonSerializerOptions options)
            => element.ValueKind switch
            {
                JsonValueKind.Object => CreateNormalizedObject(element, options),
                JsonValueKind.Array => CreateNormalizedArray(element, options),
                JsonValueKind.Null => null,
                _ => JsonValue.Create(element.Clone(), CreateNodeOptions(options)),
            };

        private static JsonNodeOptions CreateNodeOptions(JsonSerializerOptions options)
            => new() { PropertyNameCaseInsensitive = options.PropertyNameCaseInsensitive };

        public override void Write(Utf8JsonWriter writer, ActionPayload value, JsonSerializerOptions options)
        {
            value.WriteNormalizedRaw(writer, options);
        }

        private static bool IsKnownProperty(string name, JsonSerializerOptions options)
            => IsProperty(name, "type", options)
                || IsProperty(name, "channel", options)
                || IsProperty(name, "message", options)
                || IsProperty(name, "actions", options);

        private static string GetKnownPropertyName(string name, JsonSerializerOptions options)
        {
            foreach (var knownPropertyName in new[] { "type", "channel", "message", "actions" })
            {
                if (IsProperty(name, knownPropertyName, options))
                {
                    return knownPropertyName;
                }
            }

            return null;
        }

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

        private static JsonElement? GetElement(JsonNode value, JsonSerializerOptions options)
            => value == null ? null : JsonSerializer.SerializeToElement(value, options);
    }
}
