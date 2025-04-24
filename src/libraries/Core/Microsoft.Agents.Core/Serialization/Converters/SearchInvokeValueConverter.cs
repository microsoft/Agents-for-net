// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Text.Json;

namespace Microsoft.Agents.Core.Serialization.Converters
{
    internal class SearchInvokeValueConverter : ConnectorConverter<SearchInvokeValue>
    {
        /// <inheritdoc/>
        protected override bool TryReadGenericProperty(ref Utf8JsonReader reader, SearchInvokeValue value, string propertyName, JsonSerializerOptions options)
        {
            if (propertyName.Equals(nameof(value.Context), System.StringComparison.Ordinal))
            {
                SetGenericProperty(ref reader, data => value.Context = data, options);
            }
            else
            {
                return false;
            }

            return true;
        }
    }
}
