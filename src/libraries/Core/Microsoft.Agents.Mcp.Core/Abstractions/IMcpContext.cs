using Microsoft.Agents.Mcp.Core.Payloads;

namespace Microsoft.Agents.Mcp.Core.Abstractions;

public interface IMcpContext
{
    string SessionId { get; }

    // Post Methods
    Task PostNotificationAsync(McpNotification payload, CancellationToken ct);

    Task<McpPayload> PostRequestAsync(McpRequest payload, CancellationToken ct);

    Task PostResultAsync(McpResult payload, CancellationToken ct);

    Task PostErrorAsync(McpError payload, CancellationToken ct);

    Task CancelOperationAsync(string operationId, CancellationToken ct);

    McpContextProperties GetContextProperties();

    Task ApplyPropertyChangesAsync(Func<McpContextProperties, McpContextProperties> apply, CancellationToken ct);
}