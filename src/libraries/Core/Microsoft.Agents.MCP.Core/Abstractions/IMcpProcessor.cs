
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.MCP.Core.Abstractions;

public interface IMcpProcessor
{
    Task<IMcpSession> CreateSessionAsync(IMcpTransport transport, CancellationToken ct);
}