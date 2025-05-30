﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace Microsoft.Agents.Core.Serialization.Converters
{
    internal class AttachmentConverter : ConnectorConverter<Attachment>
    {
        protected override bool TryReadCollectionProperty(ref Utf8JsonReader reader, Attachment value, string propertyName, JsonSerializerOptions options)
        {
            PropertyInfo propertyInfo = typeof(Attachment).GetProperty(propertyName);
            if (propertyInfo != null && propertyInfo.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(propertyInfo.PropertyType))
            {
                return true;
            }
            return false;
        }

        protected override bool TryReadGenericProperty(ref Utf8JsonReader reader, Attachment value, string propertyName, JsonSerializerOptions options)
        {
            if (propertyName.Equals(nameof(value.Content)))
            {
                SetGenericProperty(ref reader, data => value.Content = data, options);
            }
            else
            {
                return false;
            }

            return true;
        }

        protected override void ReadExtensionData(ref Utf8JsonReader reader, Attachment value, string propertyName, JsonSerializerOptions options)
        {
            var extensionData = JsonSerializer.Deserialize<JsonElement>(ref reader, options);
            value.Properties.Add(propertyName, extensionData);
        }

        protected override bool TryReadExtensionData(ref Utf8JsonReader reader, Attachment value, string propertyName, JsonSerializerOptions options)
        {
            if (!propertyName.Equals(nameof(value.Properties)))
            {
                return false;
            }

            var propertyValue = JsonSerializer.Deserialize<object>(ref reader, options);

            foreach (var element in propertyValue.ToJsonElements())
            {
                value.Properties.Add(element.Key, element.Value);
            }

            return true;
        }

        protected override bool TryWriteExtensionData(Utf8JsonWriter writer, Attachment value, string propertyName)
        {
            if (!propertyName.Equals(nameof(value.Properties)))
            {
                return false;
            }

            foreach (var extensionData in value.Properties)
            {
                writer.WritePropertyName(extensionData.Key);
                extensionData.Value.WriteTo(writer);
            }

            return true;
        }
    }
}
