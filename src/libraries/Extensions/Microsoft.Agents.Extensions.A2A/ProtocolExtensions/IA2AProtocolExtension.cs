// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;

namespace Microsoft.Agents.Extensions.A2A.ProtocolExtensions;

internal interface IA2AProtocolExtension
{
    string Uri { get; }

    AgentExtension CreateAgentCardExtension();
}
