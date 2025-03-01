using Microsoft.Agents.MCP.Core.Payloads;
using System.Text.Json;

namespace Microsoft.Agents.MCP.Core.Abstractions;

public interface IMcpPayloadHandler
{
    public McpPayload? CreatePayload(string? id, string method, JsonElement? parameters);
    public string Method { get; }
    Task ExecuteAsync(IMcpContext context, McpPayload payload, CancellationToken ct);
}