// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Core.Serialization.Converters
{
    /// <summary>
    /// This is an attempt to handle adding type info to IDictionary string, object.  It does have a limitation of 1 nested level.
    /// </summary>
    internal class DictionaryOfObjectConverter : JsonConverter<IDictionary<string, object>>
    {
        public override IDictionary<string, object>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }
            var value = new Dictionary<string, object>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return value;
                }

                string keyString = reader.GetString();
                var itemValue = JsonSerializer.Deserialize<JsonNode>(ref reader, options);

                if (itemValue is JsonObject jObj && jObj.GetTypeInfo(out var type))
                {
                    var obj = JsonSerializer.Deserialize(itemValue, type, options);
                    
                    // Update dictionary elements with actual type
                    if (obj is IDictionary<string, object> dict)
                    {
                        foreach (KeyValuePair<string, object> child in dict)
                        {
                            var childObj = child.Value;

                            if (childObj is JsonElement element && element.ValueKind == JsonValueKind.Object)
                            {
                                childObj = JsonObject.Create(element);
                            }

                            if (childObj is JsonObject typedChild)
                            {
                                if (typedChild.GetTypeInfo(out var childType))
                                {
                                    typedChild.RemoveTypeInfo();
                                    dict[child.Key] = JsonSerializer.Deserialize(typedChild, childType, options);
                                }
                            }
                        }
                    }

                    value.Add(keyString, obj);
                }
                else
                {
                    value.Add(keyString, itemValue);
                }
            }
            throw new JsonException($"JSON did not contain the end of {typeToConvert.FullName}!");
        }

        public override void Write(Utf8JsonWriter writer, IDictionary<string, object> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (KeyValuePair<string, object> item in value)
            {
                var newValue = item.Value;
                if (item.Value == null)
                {
                    writer.WriteNull(item.Key);    
                    continue;
                }

                writer.WritePropertyName(item.Key);

                if (Convert.GetTypeCode(item.Value) == TypeCode.Object)
                {
                    newValue = JsonSerializer.SerializeToNode(item.Value, options);
                    if (newValue is JsonObject jObj)
                    {
                        jObj.AddTypeInfo(item.Value);

                        // Update dictionary elements with type info
                        if (item.Value is IDictionary<string, object> children)
                        {
                            foreach (KeyValuePair<string, object> child in children)
                            {
                                var newChildValue = JsonSerializer.SerializeToNode(child.Value, options);
                                if (newChildValue is JsonObject childJObj)
                                {
                                    childJObj.AddTypeInfo(child.Value);
                                    children[child.Key] = childJObj;
                                }
                            }
                        }
                    }
                }

                var json = JsonSerializer.Serialize(newValue, newValue.GetType(), options);
                JsonDocument.Parse(json).WriteTo(writer);
            }
            writer.WriteEndObject();
        }
    }
}
