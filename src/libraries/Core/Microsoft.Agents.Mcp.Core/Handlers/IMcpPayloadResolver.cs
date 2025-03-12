using Microsoft.Agents.Mcp.Core.Payloads;
using System.Text.Json;

namespace Microsoft.Agents.Mcp.Core.Handlers;

public interface IMcpPayloadResolver
{
    McpPayload CreateMethodRequestPayload(string? id, string method, JsonElement? parameters);
    McpPayload CreateNotificationPayload(string method, JsonElement? parameters);
}