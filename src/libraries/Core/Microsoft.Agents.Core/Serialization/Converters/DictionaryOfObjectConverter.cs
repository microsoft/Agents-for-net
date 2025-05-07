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

#if NETSTANDARD
                    var changes = new List<KeyValuePair<string, object>>();
#endif

                    // Update dictionary elements with actual type
                    if (obj is IDictionary<string, object> dict)
                    {
                        dict.RemoveTypeInfo();

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
#if NETSTANDARD
                                    changes.Add(new KeyValuePair<string, object>(child.Key, JsonSerializer.Deserialize(typedChild, childType, options)));
#else
                                    dict[child.Key] = JsonSerializer.Deserialize(typedChild, childType, options);
#endif
                                }
                            }
                            else if (childObj is JsonElement valValue)
                            {
                                switch (valValue.ValueKind)
                                {
                                    case JsonValueKind.Number:
#if NETSTANDARD
                                        changes.Add(new KeyValuePair<string, object>(child.Key, valValue.GetInt32()));
#else
                                        dict[child.Key] = valValue.GetInt32();
#endif
                                        break;

                                    case JsonValueKind.String:
#if NETSTANDARD
                                        changes.Add(new KeyValuePair<string, object>(child.Key, valValue.GetString()));
#else
                                        dict[child.Key] = valValue.GetString();
#endif
                                        break;

                                    case JsonValueKind.True:
                                    case JsonValueKind.False:
#if NETSTANDARD
                                        changes.Add(new KeyValuePair<string, object>(child.Key, valValue.GetBoolean()));
#else
                                        dict[child.Key] = valValue.GetBoolean();
#endif
                                        break;
                                }
                            }
                        }
#if NETSTANDARD
                        foreach (var change in changes)
                        {
                            dict[change.Key] = change.Value;
                        }
#endif
                    }
                    value.Add(keyString, obj);
                }
                else
                {
                    object objValue = null;
                    if (itemValue != null)
                    {
                        var valValue = itemValue.AsValue();
                        switch (valValue.GetValueKind())
                        {
                            case JsonValueKind.Number:
                                if (valValue.TryGetValue<int>(out var intValue))
                                {
                                    objValue = intValue;
                                }
                                break;

                            case JsonValueKind.String:
                                if (valValue.TryGetValue<DateTime>(out var dateValue))
                                {
                                    objValue = dateValue;
                                }
                                else if (valValue.TryGetValue<string>(out var strValue))
                                {
                                    objValue = strValue;
                                }
                                break;

                            case JsonValueKind.True:
                            case JsonValueKind.False:
                                if (valValue.TryGetValue<bool>(out var boolValue))
                                {
                                    objValue = boolValue;
                                }
                                break;

                            default:
                                objValue = itemValue; break;
                        }
                    }
                    value.Add(keyString, objValue);
                }
            }
            throw new JsonException($"JSON did not contain the end of {typeToConvert.FullName}!");
        }

        public override void Write(Utf8JsonWriter writer, IDictionary<string, object> value, JsonSerializerOptions options)
        {
            // If the dictionary is empty, avoid creating an empty object so it doesn't fail at serialization.
            if (value.Count == 0)
            {
                return;
            }

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
