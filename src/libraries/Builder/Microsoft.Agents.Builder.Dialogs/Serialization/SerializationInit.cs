// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Builder.Dialogs.Serialization
{
    [SerializationInit]
    internal class SerializationInit
    {
        public static void Init()
        {
            var converters = new List<JsonConverter>
            {
                new PersistedStateConverter(),
                new Array2DConverter()
            };

            ProtocolJsonSerializer.ApplyExtensionConverters(converters);
        }
    }
}
