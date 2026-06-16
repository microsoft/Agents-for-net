// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.SharePoint.Serialization.Converters;

namespace Microsoft.Agents.Extensions.SharePoint.Serialization
{
    [SerializationInit]
    internal class SerializationInit
    {
        public static void Init()
        {
            ProtocolJsonSerializer.ApplyExtensionConverters(
            [
                new AceDataConverter(),
                new AceRequestConverter()
            ]);
        }
    }
}
