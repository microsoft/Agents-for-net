// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.Json;
using System;
using System.Reflection;
using System.Collections;

namespace Microsoft.Agents.Core.Serialization.Converters
{
    public abstract class ConnectorConverter<T> : JsonConverter<T> where T : new()
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"JSON is not at the start of {typeToConvert.FullName}!");
            }

            var value = new T();

            var properties = options.PropertyNameCaseInsensitive
                ? new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, PropertyInfo>();

            foreach (var property in typeof(T).GetProperties())
            {
                properties.Add(property.Name, property);
            }

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return value;
                }

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();

                    if (properties.ContainsKey(propertyName))
                    {
                        ReadProperty(ref reader, value, propertyName, options, properties);
                    }
                    else
                    {
                        ReadExtensionData(ref reader, value, propertyName, options);
                    }
                }
            }

            throw new JsonException($"JSON did not contain the end of {typeToConvert.FullName}!");
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            foreach (var property in value.GetType().GetProperties())
            {
                if (!TryWriteExtensionData(writer, value, property.Name))
                {
                    var propertyValue = property.GetValue(value);

                    if (propertyValue is IList list)
                    {
                        if (list == null || list.Count == 0)
                        {
                            continue;
                        }
                    }

                    if (propertyValue != null || !(options.DefaultIgnoreCondition == JsonIgnoreCondition.WhenWritingNull))

                    {
                        var propertyName = options.PropertyNamingPolicy == JsonNamingPolicy.CamelCase
                            ? JsonNamingPolicy.CamelCase.ConvertName(property.Name)
                            : property.Name;

                        writer.WritePropertyName(propertyName);

                        if (property.PropertyType == typeof(object) && propertyValue is string s)
                        {
                            // Generic property value as a JSON string
                            try
                            {
                                using (var document = JsonDocument.Parse(s))
                                {
                                    var root = document.RootElement.Clone();
                                    if (root.ValueKind == JsonValueKind.Object)
                                    {
                                        root.WriteTo(writer);
                                    }
                                    else
                                    {
                                        writer.WriteStringValue(s);
                                    }
                                }
                            }
                            catch (JsonException)
                            {
                                writer.WriteStringValue(s);
                            }
                        }
                        else
                        {
                            var json = System.Text.Json.JsonSerializer.Serialize(propertyValue, propertyValue?.GetType() ?? property.PropertyType, options);
                            JsonDocument.Parse(json).WriteTo(writer);
                        }
                    }
                }
            }

            writer.WriteEndObject();
        }

        protected virtual bool TryReadCollectionProperty(ref Utf8JsonReader reader, T value, string propertyName, JsonSerializerOptions options)
        {
            return false;
        }

        protected virtual bool TryReadGenericProperty(ref Utf8JsonReader reader, T value, string propertyName, JsonSerializerOptions options)
        {
            return false;
        }

        protected virtual void ReadExtensionData(ref Utf8JsonReader reader, T value, string propertyName, JsonSerializerOptions options)
        {
        }

        /// <summary>
        /// Handle undefined properties in JSON without the need for the JsonExtensionData annotation.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="value"></param>
        /// <param name="propertyName"></param>
        /// <param name="options"></param>
        protected virtual bool TryReadExtensionData(ref Utf8JsonReader reader, T value, string propertyName, JsonSerializerOptions options)
        {
            return false;
        }

        /// <summary>
        /// Handle undefined properties in JSON without the need for the JsonExtensionData annotation.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="value"></param>
        /// <param name="propertyName"></param>
        /// <param name="writer"></param>
        /// <param name="options"></param>
        protected virtual bool TryWriteExtensionData(Utf8JsonWriter writer, T value, string propertyName)
        {
            return false;
        }

        protected void SetCollection<TCollection>(ref Utf8JsonReader reader, IList<TCollection> collection, JsonSerializerOptions options)
        {
            collection.Clear();

            var items = System.Text.Json.JsonSerializer.Deserialize<IList<TCollection>>(ref reader, options);

            if (items != null)
            {
                foreach (var item in items)
                {
                    collection.Add(item);
                }
            }
        }

        /// <summary>
        /// This is to handle 'object' type properties that must conform to the original BF SDK handling.
        /// A simple type (string, int, etc...) is set as the value.  Complex objects are of type 'JsonElement'.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="setter"></param>
        /// <param name="options"></param>
        protected void SetGenericProperty(ref Utf8JsonReader reader, Action<object> setter, JsonSerializerOptions options)
        {
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<object>(ref reader, options);

            if (deserialized == null)
            {
                return;
            }

            if (deserialized is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.String)
                {
                    var json = element.GetString();

                    try
                    {
                        // Check if the underlying JSON is a reference type
                        using (var document = JsonDocument.Parse(json))
                        {
                            setter(document.RootElement.Clone());
                            if (document.RootElement.ValueKind == JsonValueKind.Object || document.RootElement.ValueKind == JsonValueKind.Array)
                            {
                                setter(document.RootElement.Clone());
                            }
                            else
                            {
                                setter(element.GetString());
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        // JSON is a value type
                        setter(element.GetString());
                    }
                }
                else if (element.ValueKind == JsonValueKind.Number)
                {
                    setter(element.GetInt32());
                }
                else if (element.ValueKind == JsonValueKind.True)
                {
                    setter(true);
                }
                else if (element.ValueKind == JsonValueKind.False)
                {
                    setter(false);
                }
                else
                {
                    setter(element);
                }

                return;
            }

            setter(deserialized);
        }

        protected virtual void ReadProperty(ref Utf8JsonReader reader, T value, string propertyName, JsonSerializerOptions options, Dictionary<string, PropertyInfo> properties)
        {
            var property = properties[propertyName];

            if (TryReadExtensionData(ref reader, value, property.Name, options))
            {
                return;
            }

            if (TryReadCollectionProperty(ref reader, value, property.Name, options))
            {
                var CollectionPropertyValue = System.Text.Json.JsonSerializer.Deserialize(ref reader, property.PropertyType, options);
                if (CollectionPropertyValue is IList prospectiveList)
                {
                    if (prospectiveList.Count != 0)
                    {
                        property.SetValue(value, CollectionPropertyValue);
                    }
                    else
                    {
                        property.SetValue(value, null);
                    }
                }
                return;
            }

            if (TryReadGenericProperty(ref reader, value, property.Name, options))
            {
                return;
            }

            var propertyValue = System.Text.Json.JsonSerializer.Deserialize(ref reader, property.PropertyType, options);
            property.SetValue(value, propertyValue);
        }
    }
}
