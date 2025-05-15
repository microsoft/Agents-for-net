// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Text.Json;

namespace Microsoft.Agents.Core.Serialization.Converters
{
    internal class AIEntityConverter : ConnectorConverter<AIEntity>
    {
        protected override void ReadExtensionData(ref Utf8JsonReader reader, AIEntity value, string propertyName, JsonSerializerOptions options)
        {
            if (propertyName.Equals("@type"))
            {
                value.AtType = JsonSerializer.Deserialize<string>(ref reader, options);
            }
            else if (propertyName.Equals("@context"))
            {
                value.AtContext = JsonSerializer.Deserialize<string>(ref reader, options);
            }
            else if (propertyName.Equals("@id"))
            {
                value.AtId = JsonSerializer.Deserialize<string>(ref reader, options);
            }
            else if (propertyName.Equals(nameof(value.Properties)))
            {
                value.Properties.Add(propertyName, JsonSerializer.Deserialize<JsonElement>(ref reader, options));
            }
        }

        protected override bool TryReadExtensionData(ref Utf8JsonReader reader, AIEntity value, string propertyName, JsonSerializerOptions options)
        {
            if (propertyName.Equals(nameof(value.Properties)))
            {
                var propertyValue = JsonSerializer.Deserialize<object>(ref reader, options);

                foreach (var element in propertyValue.ToJsonElements())
                {
                    value.Properties.Add(element.Key, element.Value);
                }

                return true;
            }

            return false;
        }

        protected override bool TryWriteExtensionData(Utf8JsonWriter writer, AIEntity value, string propertyName)
        {
            if (propertyName.Equals(nameof(value.Properties)))
            {
                foreach (var extensionData in value.Properties)
                {
                    writer.WritePropertyName(extensionData.Key);
                    extensionData.Value.WriteTo(writer);
                }

                return true;
            }
            else if (propertyName.Equals(nameof(value.AtType)))
            {
                writer.WritePropertyName("@type");
                writer.WriteStringValue(value.AtType);
                return true;
            }
            else if (propertyName.Equals(nameof(value.AtContext)))
            {
                writer.WritePropertyName("@context");
                writer.WriteStringValue(value.AtContext);
                return true;
            }
            else if (propertyName.Equals(nameof(value.AtId)))
            {
                writer.WritePropertyName("@id");
                writer.WriteStringValue(value.AtId);
                return true;
            }

            return false;
        }
    }
}
