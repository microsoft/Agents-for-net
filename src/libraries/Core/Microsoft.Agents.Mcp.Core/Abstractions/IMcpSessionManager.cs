namespace Microsoft.Agents.Mcp.Core.Abstractions;

public interface IMcpSessionManager
{
    Task<IMcpSession> CreateSessionAsync(IMcpTransport transport, CancellationToken ct);
}