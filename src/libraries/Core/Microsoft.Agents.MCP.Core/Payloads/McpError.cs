using Microsoft.Agents.MCP.Core.JsonRpc;

namespace Microsoft.Agents.MCP.Core.Payloads;

public class McpError : McpPayload
{
    public string? Id { get; init; }

    public JsonRpcError? Error { get; init; }
}