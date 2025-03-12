using Microsoft.Agents.Mcp.Core.Payloads;

namespace Microsoft.Agents.Mcp.Core.Abstractions;

public interface IMcpHandler
{
    Task HandleAsync(IMcpSession session, McpPayload payload, CancellationToken ct);
}