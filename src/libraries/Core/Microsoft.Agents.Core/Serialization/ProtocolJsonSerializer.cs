// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Agents.Core.Serialization.Converters;
using Microsoft.Agents.Core.SharePoint.Serialization.Converters;
using Microsoft.Agents.Core.Teams.Serialization.Converters;

namespace Microsoft.Agents.Core.Serialization
{
    /// <summary>
    /// Extensions for converting objects to desired types using serialization.
    /// </summary>
    public static class ProtocolJsonSerializer
    {
        public const string ApplicationJson = "application/json";
        public static JsonSerializerOptions SerializationOptions = CreateConnectorOptions();

        public static JsonSerializerOptions CreateConnectorOptions()
        {
            var options = new JsonSerializerOptions()
                .ApplyCoreOptions()
                .ApplyTeamsOptions()
                .ApplySharepointOptions();

            return options;
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
            options.Converters.Add(new EntityConverter());
            options.Converters.Add(new TokenExchangeInvokeResponseConverter());
            options.Converters.Add(new TokenExchangeInvokeRequestConverter());
            options.Converters.Add(new TokenResponseConverter());
            options.Converters.Add(new VideoCardConverter());
            options.Converters.Add(new Array2DConverter());
            options.Converters.Add(new DictionaryOfObjectConverter());
            options.Converters.Add(new SuggestedActionsConverter());
            options.Converters.Add(new SearchInvokeValueConverter());

            return options;
        }

        private static JsonSerializerOptions ApplyTeamsOptions(this JsonSerializerOptions options)
        {
            options.Converters.Add(new SurfaceConverter());
            options.Converters.Add(new TabSubmitDataConverter());
            options.Converters.Add(new TeamsChannelDataConverter());
            options.Converters.Add(new MessagingExtensionActionResponseConverter());
            options.Converters.Add(new TaskModuleResponseConverter());
            options.Converters.Add(new TaskModuleResponseBaseConverter());
            options.Converters.Add(new TaskModuleCardResponseConverter());
            options.Converters.Add(new TaskModuleContinueResponseConverter());
            options.Converters.Add(new TaskModuleMessageResponseConverter());
            options.Converters.Add(new MessagingExtensionAttachmentConverter());

            return options;
        }

        private static JsonSerializerOptions ApplySharepointOptions(this JsonSerializerOptions options)
        {
            options.Converters.Add(new AceDataConverter());
            options.Converters.Add(new AceRequestConverter());

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
                    using (var document = JsonDocument.Parse(json))
                    {
                        foreach (var property in document.RootElement.Clone().EnumerateObject())
                        {
                            elements.Add(property.Name, property.Value);
                        }
                    }
                }
            }
            else
            {
                var serialized = System.Text.Json.JsonSerializer.Serialize(value, SerializationOptions);
                using (var document = JsonDocument.Parse(serialized))
                {
                    foreach (var property in document.RootElement.Clone().EnumerateObject())
                    {
                        elements.Add(property.Name, property.Value);
                    }
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
        /// <typeparam name="T">The type of object to convert to.</typeparam>
        /// <returns>The converted object.</returns>
        public static T ToObject<T>(object value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
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

                return System.Text.Json.JsonSerializer.Deserialize<T>(json, SerializationOptions);
            }
            else if (value is Stream stream)
            {
                return System.Text.Json.JsonSerializer.Deserialize<T>(stream, ProtocolJsonSerializer.SerializationOptions);
            }

            var serialized = System.Text.Json.JsonSerializer.Serialize(value, SerializationOptions);
            return System.Text.Json.JsonSerializer.Deserialize<T>(serialized, SerializationOptions);
        }



        public static T CloneTo<T>(object obj)
        {
            return System.Text.Json.JsonSerializer.Deserialize<T>(System.Text.Json.JsonSerializer.Serialize(obj, ProtocolJsonSerializer.SerializationOptions), ProtocolJsonSerializer.SerializationOptions);
        }

        public static string ToJson(object value)
        {
            return System.Text.Json.JsonSerializer.Serialize(value, ProtocolJsonSerializer.SerializationOptions);
        }

        public static ToT GetAs<ToT, FromT>(FromT source)
        {
            return System.Text.Json.JsonSerializer.Deserialize<ToT>(System.Text.Json.JsonSerializer.Serialize(source, ProtocolJsonSerializer.SerializationOptions), ProtocolJsonSerializer.SerializationOptions);
        }

        public static bool JsonEquals(object left, object right)
        {
            return System.Text.Json.JsonSerializer.Serialize(left).Equals(System.Text.Json.JsonSerializer.Serialize(right), StringComparison.Ordinal);
        }
    }
}
