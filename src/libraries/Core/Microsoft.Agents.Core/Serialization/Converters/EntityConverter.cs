// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System;
using System.Text.Json;

namespace Microsoft.Agents.Core.Serialization.Converters
{
    internal class EntityConverter : ConnectorConverter<Entity>
    {
        public override Entity Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var entity = base.Read(ref reader, typeToConvert, options);

            if (string.Equals(EntityTypes.Mention, entity.Type, StringComparison.OrdinalIgnoreCase))
            {
                return JsonSerializer.Deserialize<Mention>(JsonSerializer.Serialize(entity, options), options);
            }
            else if (string.Equals(EntityTypes.Place, entity.Type, StringComparison.OrdinalIgnoreCase))
            {
                return JsonSerializer.Deserialize<Place>(JsonSerializer.Serialize(entity, options), options);
            }
            else if (string.Equals(EntityTypes.Thing, entity.Type, StringComparison.OrdinalIgnoreCase))
            {
                return JsonSerializer.Deserialize<Thing>(JsonSerializer.Serialize(entity, options), options);
            }
            else if (string.Equals(EntityTypes.GeoCoordinates, entity.Type, StringComparison.OrdinalIgnoreCase))
            {
                return JsonSerializer.Deserialize<GeoCoordinates>(JsonSerializer.Serialize(entity, options), options);
            }
            else if (string.Equals(EntityTypes.StreamInfo, entity.Type, StringComparison.OrdinalIgnoreCase))
            {
                return JsonSerializer.Deserialize<StreamInfo>(JsonSerializer.Serialize(entity, options), options);
            }
            else if (string.Equals(EntityTypes.ActivityTreatment, entity.Type, StringComparison.OrdinalIgnoreCase))
            {
                return JsonSerializer.Deserialize<ActivityTreatment>(JsonSerializer.Serialize(entity, options), options);
            }
			else if (string.Equals(EntityTypes.ProductInfo, entity.Type, StringComparison.OrdinalIgnoreCase))
            {
                return JsonSerializer.Deserialize<ProductInfo>(JsonSerializer.Serialize(entity, options), options);
            }
            
            else if (string.Equals(EntityTypes.AICitation, entity.Type, StringComparison.OrdinalIgnoreCase))
            {
                return JsonSerializer.Deserialize<AIEntity>(JsonSerializer.Serialize(entity, options), options);
            }

            return entity;
        }

        protected override void ReadExtensionData(ref Utf8JsonReader reader, Entity value, string propertyName, JsonSerializerOptions options)
        {
            var extensionData = JsonSerializer.Deserialize<JsonElement>(ref reader, options);
            value.Properties.Add(propertyName, extensionData);
        }

        protected override bool TryReadExtensionData(ref Utf8JsonReader reader, Entity value, string propertyName, JsonSerializerOptions options)
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

        protected override bool TryWriteExtensionData(Utf8JsonWriter writer, Entity value, string propertyName)
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

            return false;
        }
    }
}
