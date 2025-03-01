using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.MCP.Core.Payloads;

namespace Microsoft.Agents.MCP.Core.Abstractions;

public interface IMcpSession
{
    IAsyncEnumerable<McpPayload> GetSessionStream(CancellationToken ct);

    Task Close(CancellationToken ct);

    Task WriteOutgoingPayload(McpPayload payload, CancellationToken ct);

    IAsyncEnumerable<McpPayload> GetIncomingSessionStream(CancellationToken ct);
    McpContextProperties GetContextProperties();
    Task ApplyPropertyChangesAsync(Func<McpContextProperties, McpContextProperties> apply, CancellationToken ct);

    string SessionId { get; }
}

