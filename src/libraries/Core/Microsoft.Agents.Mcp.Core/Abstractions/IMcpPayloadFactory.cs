using Microsoft.Agents.Mcp.Core.JsonRpc;
using Microsoft.Agents.Mcp.Core.Payloads;

namespace Microsoft.Agents.Mcp.Core.Abstractions;

public interface IMcpPayloadFactory
{
    McpPayload CreatePayload(JsonRpcPayload jsonRpcPayload);

    JsonRpcPayload CreateJsonRpcPayload(McpPayload McpPayload);
}