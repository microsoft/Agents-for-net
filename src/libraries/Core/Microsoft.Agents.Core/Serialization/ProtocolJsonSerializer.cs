// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Agents.Core.Serialization.Converters;

namespace Microsoft.Agents.Core.Serialization
{
    /// <summary>
    /// Extensions for converting objects to desired types using serialization.
    /// </summary>
    public static class ProtocolJsonSerializer
    {
        public const string ApplicationJson = "application/json";
        public static JsonSerializerOptions SerializationOptions { get; private set; } = CreateConnectorOptions();
        public static bool UnpackObjectStrings { get; set; } = true;
        public static bool SubChannelSupport { get; set; } = true;

        private static readonly object _optionsLock = new object();

        static ProtocolJsonSerializer()
        {
            SerializationInitAttribute.InitSerialization();
        }

        private static JsonSerializerOptions CreateConnectorOptions()
        {
            var options = new JsonSerializerOptions()
                .ApplyCoreOptions();

            return options;
        }

        public static void ApplyExtensionConverters(IList<JsonConverter> extensionConverters)
        {
            lock(_optionsLock)
            {
                var newOptions = SerializationOptions;
                if (newOptions.IsReadOnly)
                {
                    newOptions = new JsonSerializerOptions(SerializationOptions);
                }

                foreach (var converter in extensionConverters)
                {
                    newOptions.Converters.Add(converter);
                }

                SerializationOptions = newOptions;
            }
        }

        public static void ApplyExtensionOptions(Func<JsonSerializerOptions, JsonSerializerOptions> applyFunc)
        {
            lock (_optionsLock)
            {
                var newOptions = SerializationOptions;
                if (newOptions.IsReadOnly)
                {
                    newOptions = new JsonSerializerOptions(SerializationOptions);
                }

                SerializationOptions = applyFunc(newOptions);
            }
        }

        private static JsonSerializerOptions ApplyCoreOptions(this JsonSerializerOptions options)
        {
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.PropertyNameCaseInsensitive = true;
            options.IncludeFields = true;
            options.NumberHandling = JsonNumberHandling.AllowReadingFromString;
            //options.UnknownTypeHandling = JsonUnknownTypeHandling.JsonNode;

            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

            options.Converters.Add(new ActivityConverter());
            options.Converters.Add(new IActivityConverter());
            options.Converters.Add(new AnimationCardConverter());
            options.Converters.Add(new AttachmentConverter());
            options.Converters.Add(new AudioCardConverter());
            options.Converters.Add(new CardActionConverter());
            options.Converters.Add(new ChannelAccountConverter());
            options.Converters.Add(new ConversationAccountConverter());
            options.Converters.Add(new EntityConverter());
            options.Converters.Add(new AIEntityConverter());
            options.Converters.Add(new TokenExchangeInvokeResponseConverter());
            options.Converters.Add(new TokenExchangeInvokeRequestConverter());
            options.Converters.Add(new TokenResponseConverter());
            options.Converters.Add(new VideoCardConverter());
            options.Converters.Add(new Array2DConverter());
            options.Converters.Add(new DictionaryOfObjectConverter());
            options.Converters.Add(new SuggestedActionsConverter());
            options.Converters.Add(new AdaptiveCardInvokeResponseConverter());

            return options;
        }

        /// <summary>
        /// Decompose an object into its constituent JSON elements.
        /// </summary>
        /// <param name="value">The object to be decomposed into JSON elements.</param>
        /// <returns>A dictionary of JSON elements keyed by property name.</returns>
        public static IDictionary<string, JsonElement> ToJsonElements(this object value)
        {
            if (value == null)
            {
                return new Dictionary<string, JsonElement>();
            }

            if (value is Dictionary<string, JsonElement> result)
            {
                return result;
            }

            var elements = new Dictionary<string, JsonElement>();

            if (value is string json)
            {
                if (!string.IsNullOrWhiteSpace(json))
                {
                    using var document = JsonDocument.Parse(json);
                    foreach (var property in document.RootElement.Clone().EnumerateObject())
                    {
                        elements.Add(property.Name, property.Value);
                    }
                }
            }
            else
            {
                var serialized = JsonSerializer.Serialize(value, SerializationOptions);
                using var document = JsonDocument.Parse(serialized);
                foreach (var property in document.RootElement.Clone().EnumerateObject())
                {
                    elements.Add(property.Name, property.Value);
                }
            }

            return elements;
        }

        public static void Add(this IDictionary<string, JsonElement> target, object value)
        {
            var elements = value.ToJsonElements();
            foreach (var prop in elements)
            {
                target.Add(prop.Key, prop.Value);
            }
        }

        /// <summary>
        /// Convert an object to the desired type using serialization and deserialization.
        /// </summary>
        /// <param name="value">The object to be converted to desired type: string, MemoryStream, object</param>
        /// <param name="defaultFactory"></param>
        /// <typeparam name="T">The type of object to convert to.</typeparam>
        /// <returns>The converted object.</returns>
        public static T ToObject<T>(object value, Func<T> defaultFactory = null)
        {
            if (value == null)
            {
                if (defaultFactory != null)
                {
                    return defaultFactory();
                }

                return default;
            }

            if (value is T result)
            {
                return result;
            }

            if (value is string json)
            {
                if (string.IsNullOrWhiteSpace(json))
                {
                    throw new ArgumentNullException(nameof(value));
                }

                return JsonSerializer.Deserialize<T>(json, SerializationOptions);
            }
            else if (value is Stream stream)
            {
                return JsonSerializer.Deserialize<T>(stream, SerializationOptions);
            }

            var serialized = JsonSerializer.Serialize(value, SerializationOptions);
            return JsonSerializer.Deserialize<T>(serialized, SerializationOptions);
        }

        public static bool Equals<T>(T value1, T value2)
        {
            return string.Equals(
                    JsonSerializer.Serialize(value1, SerializationOptions),
                    JsonSerializer.Serialize(value2, SerializationOptions)
                );
        }

        public static T CloneTo<T>(object obj)
        {
            return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(obj, SerializationOptions), SerializationOptions);
        }

        public static string ToJson(object value)
        {
            return JsonSerializer.Serialize(value, SerializationOptions);
        }

        public static ToT GetAs<ToT, FromT>(FromT source)
        {
            return JsonSerializer.Deserialize<ToT>(JsonSerializer.Serialize(source, SerializationOptions), SerializationOptions);
        }
    }
}
