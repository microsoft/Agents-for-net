// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace Microsoft.Agents.Core.Serialization.Converters
{
    // This class is used to convert the 'SuggestedActions' property of type SuggestedActions for Teams.
    internal class SuggestedActionsConverter : ConnectorConverter<SuggestedActions>
    {
        protected override bool TryReadCollectionProperty(ref Utf8JsonReader reader, SuggestedActions value, string propertyName, JsonSerializerOptions options)
        {
            PropertyInfo propertyInfo = typeof(SuggestedActions).GetProperty(propertyName);
            if (propertyInfo != null && propertyInfo.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(propertyInfo.PropertyType))
            {
                return true;
            }
            return base.TryReadCollectionProperty(ref reader, value, propertyName, options);
        }
    }
}
