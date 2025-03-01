namespace Microsoft.Agents.MCP.Core.Abstractions;

public interface IMcpSessionManager
{
    Task<IMcpSession> CreateSessionAsync(IMcpTransport transport, CancellationToken ct);
}