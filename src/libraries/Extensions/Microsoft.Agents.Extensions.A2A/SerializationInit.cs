// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using Microsoft.Extensions.AI;

namespace Microsoft.Agents.Extensions.A2A;

[SerializationInit]
internal class SerializationInit
{
    public static void Init()
    {
        ProtocolJsonSerializer.AddTypeInfoResolver(AIJsonUtilities.DefaultOptions.TypeInfoResolver);
    }
}
