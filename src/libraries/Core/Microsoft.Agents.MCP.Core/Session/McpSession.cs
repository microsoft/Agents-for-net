using Microsoft.Agents.MCP.Core.Abstractions;
using Microsoft.Agents.MCP.Core.Payloads;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.MCP.Core.Session;

public record McpSession(
    Func<CancellationToken, IAsyncEnumerable<McpPayload>> SessionStreamReader,
    Func<CancellationToken, IAsyncEnumerable<McpPayload>> IncomingSessionStreamReader,
    Func<McpPayload, CancellationToken, Task> SessionStreamWriter,
    Func<CancellationToken, Task> CloseFunction,
    Func<Func<McpContextProperties, McpContextProperties>, CancellationToken, Task> ApplyContext,
    Func<McpContextProperties> GetContext,
    string SessionId) : IMcpSession
{
    public Task ApplyPropertyChangesAsync(Func<McpContextProperties, McpContextProperties> apply, CancellationToken ct) => ApplyContext(apply, ct);

    public McpContextProperties GetContextProperties() => GetContext();

    public Task Close(CancellationToken ct) => CloseFunction(ct);

    public IAsyncEnumerable<McpPayload> GetIncomingSessionStream(CancellationToken ct) => IncomingSessionStreamReader(ct);

    public IAsyncEnumerable<McpPayload> GetSessionStream(CancellationToken ct) => SessionStreamReader(ct);

    public Task WriteOutgoingPayload(McpPayload payload, CancellationToken ct) => SessionStreamWriter(payload, ct);
}
