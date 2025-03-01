using Microsoft.Agents.MCP.Core.JsonRpc;

namespace Microsoft.Agents.MCP.Core.Abstractions;

public interface IMcpTransport
{
    bool IsClosed { get; }
    Task CloseAsync(CancellationToken ct);
    Task Connect(string sessionId, Func<JsonRpcPayload, CancellationToken, Task> ingestMessage, Func<CancellationToken, Task> close);
    Task SendOutgoingAsync(JsonRpcPayload payload, CancellationToken ct);
    Task ProcessPayloadAsync(JsonRpcPayload payload, CancellationToken ct);
}