using Microsoft.Agents.MCP.Core.Payloads;
using System.Text.Json;

namespace Microsoft.Agents.MCP.Core.Handlers;

public interface IMcpPayloadResolver
{
    McpPayload CreateMethodRequestPayload(string? id, string method, JsonElement? parameters);
    McpPayload CreateNotificationPayload(string method, JsonElement? parameters);
}