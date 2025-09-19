// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using System.Text.Json.Serialization.Metadata;
using static Microsoft.Agents.Hosting.A2A.A2AJsonUtilities;

namespace Microsoft.Agents.Hosting.AspNetCore.A2A
{
    internal class SerializationInit
    {
        public static void Init()
        {
            // Enable reflection fallback
            ProtocolJsonSerializer.ApplyExtensionOptions(options =>
            {
                options.TypeInfoResolver = JsonTypeInfoResolver.Combine(JsonContext.Default, new DefaultJsonTypeInfoResolver());
                return options;
            });
        }
    }
}
