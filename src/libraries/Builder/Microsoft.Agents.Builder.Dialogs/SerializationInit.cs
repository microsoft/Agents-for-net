// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Core.Serialization.Converters;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Builder.Dialogs
{
    [SerializationInit]
    internal class SerializationInit
    {
        public static void Init()
        {
            var converters = new List<JsonConverter>
            {
                new PersistedStateConverter(),
            };

            ProtocolJsonSerializer.ApplyExtensionConverters(converters);
        }
    }
}
