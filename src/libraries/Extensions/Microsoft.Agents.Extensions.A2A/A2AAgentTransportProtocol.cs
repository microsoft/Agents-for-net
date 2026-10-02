// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.A2A;

/// <summary>
/// Defines protocol-binding identifiers supported by A2A agent interfaces.
/// </summary>
public static class A2AAgentTransportProtocol
{
    /// <summary>
    /// The JSON-RPC protocol-binding identifier.
    /// </summary>
    public const string JsonRpc = "JSONRPC";

    /// <summary>
    /// The HTTP+JSON protocol-binding identifier.
    /// </summary>
    public const string HttpJson = "HTTP+JSON";
}
