﻿using Microsoft.Agents.Mcp.Core.JsonRpc;

namespace Microsoft.Agents.Mcp.Core.Payloads;

public class McpError : McpPayload
{
    public string? Id { get; init; }

    public JsonRpcError? Error { get; init; }
}