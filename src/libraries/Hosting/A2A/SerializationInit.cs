// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using Microsoft.Extensions.AI;
using System.Text.Json.Serialization.Metadata;

namespace Microsoft.Agents.Hosting.AspNetCore.A2A
{
    [SerializationInit]
    internal class SerializationInit
    {
        public static void Init()
        {
            // Enable reflection fallback
            ProtocolJsonSerializer.ApplyExtensionOptions(options =>
            {
                options.TypeInfoResolver = JsonTypeInfoResolver.Combine(AIJsonUtilities.DefaultOptions.TypeInfoResolver, new DefaultJsonTypeInfoResolver());
                return options;
            });
        }
    }
}
