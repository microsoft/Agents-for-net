using Microsoft.Agents.Mcp.Core.JsonRpc;

namespace Microsoft.Agents.Mcp.Core.Abstractions;

public interface IMcpTransport
{
    bool IsClosed { get; }
    Task CloseAsync(CancellationToken ct);
    Task Connect(string sessionId, Func<JsonRpcPayload, CancellationToken, Task> ingestMessage, Func<CancellationToken, Task> close);
    Task SendOutgoingAsync(JsonRpcPayload payload, CancellationToken ct);
    Task ProcessPayloadAsync(JsonRpcPayload payload, CancellationToken ct);
}