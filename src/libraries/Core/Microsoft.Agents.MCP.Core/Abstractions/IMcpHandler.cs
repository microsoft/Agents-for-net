using Microsoft.Agents.MCP.Core.Payloads;

namespace Microsoft.Agents.MCP.Core.Abstractions;

public interface IMcpHandler
{
    Task HandleAsync(IMcpSession session, McpPayload payload, CancellationToken ct);
}