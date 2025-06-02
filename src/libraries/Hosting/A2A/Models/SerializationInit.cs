// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using System.Text.Json;

namespace Microsoft.Agents.Hosting.A2A.Models
{
    [SerializationInit]
    internal class SerializationInit
    {
        public static void Init()
        {
            ProtocolJsonSerializer.ApplyExtensionOptions((inOptions) =>
            {
                return new JsonSerializerOptions(inOptions)
                {
                    AllowOutOfOrderMetadataProperties = true,
                };
            });
        }
    }
}
