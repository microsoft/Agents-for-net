
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Mcp.Core.Abstractions;

public interface IMcpProcessor
{
    Task<IMcpSession> CreateSessionAsync(IMcpTransport transport, CancellationToken ct);
}