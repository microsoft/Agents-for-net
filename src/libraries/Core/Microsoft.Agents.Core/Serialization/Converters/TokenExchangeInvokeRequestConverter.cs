// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Text.Json;

namespace Microsoft.Agents.Core.Serialization.Converters
{
    internal class TokenExchangeInvokeRequestConverter : ConnectorConverter<TokenExchangeInvokeRequest>
    {
        protected override void ReadExtensionData(ref Utf8JsonReader reader, TokenExchangeInvokeRequest value, string propertyName, JsonSerializerOptions options)
        {
            var extensionData = JsonSerializer.Deserialize<JsonElement>(ref reader, options);
            value.Properties.Add(propertyName, extensionData);
        }

        protected override bool TryReadExtensionData(ref Utf8JsonReader reader, TokenExchangeInvokeRequest value, string propertyName, JsonSerializerOptions options)
        {
            if (propertyName.Equals(nameof(value.Properties), System.StringComparison.Ordinal))
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

        protected override bool TryWriteExtensionData(Utf8JsonWriter writer, TokenExchangeInvokeRequest value, string propertyName)
        {
            if (propertyName.Equals(nameof(value.Properties), System.StringComparison.Ordinal))
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
