using Microsoft.Agents.MCP.Core.JsonRpc;
using Microsoft.Agents.MCP.Core.Payloads;

namespace Microsoft.Agents.MCP.Core.Abstractions;

public interface IMcpPayloadFactory
{
    McpPayload CreatePayload(JsonRpcPayload jsonRpcPayload);

    JsonRpcPayload CreateJsonRpcPayload(McpPayload mcpPayload);
}