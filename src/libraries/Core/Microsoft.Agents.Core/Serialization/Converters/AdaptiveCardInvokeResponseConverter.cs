// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Text.Json;

namespace Microsoft.Agents.Core.Serialization.Converters
{
    internal class AdaptiveCardInvokeResponseConverter : ConnectorConverter<AdaptiveCardInvokeResponse>
    {
        protected override bool TryReadGenericProperty(ref Utf8JsonReader reader, AdaptiveCardInvokeResponse value, string propertyName, JsonSerializerOptions options)
        {
            if (propertyName.Equals(nameof(value.Value)))
            {
                SetGenericProperty(ref reader, data => value.Value = data, options);
            }
            else
            {
                return false;
            }

            return true;
        }
    }
}
