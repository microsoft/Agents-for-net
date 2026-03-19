// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace Microsoft.Agents.Core.Serialization.Converters
{
    internal class CardActionConverter : ConnectorConverter<CardAction>
    {
        protected override bool TryReadCollectionProperty(ref Utf8JsonReader reader, CardAction value, string propertyName, JsonSerializerOptions options)
        {
            PropertyInfo propertyInfo = typeof(CardAction).GetProperty(propertyName);
            if (propertyInfo != null && propertyInfo.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(propertyInfo.PropertyType))
            {
                return true;
            }
            return false;
        }

        protected override bool TryReadGenericProperty(ref Utf8JsonReader reader, CardAction value, string propertyName, JsonSerializerOptions options)
        {
            if (propertyName.Equals(nameof(value.ChannelData)))
            {
                SetGenericProperty(ref reader, data => value.ChannelData = data, options);
            }
            else if (propertyName.Equals(nameof(value.Value)))
            {
                SetGenericProperty(ref reader, data => value.Value = data, options);
            }
            else
            {
                return false;
            }

            return true;
        }

        protected override void ReadExtensionData(ref Utf8JsonReader reader, CardAction value, string propertyName, JsonSerializerOptions options)
        {
        }

        protected override bool TryReadExtensionData(ref Utf8JsonReader reader, CardAction value, string propertyName, JsonSerializerOptions options)
        {
            return false;
        }

        protected override bool TryWriteExtensionData(Utf8JsonWriter writer, CardAction value, string propertyName)
        {
            return false;
        }
    }
}
