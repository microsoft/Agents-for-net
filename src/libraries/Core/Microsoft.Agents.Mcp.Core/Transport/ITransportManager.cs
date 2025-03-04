using System.Diagnostics.CodeAnalysis;
using Microsoft.Agents.Mcp.Core.Abstractions;

namespace Microsoft.Agents.Mcp.Core.Transport
{
    public interface ITransportManager
    {
        public bool AddTransport(string sessionId, IMcpTransport transport);
        public bool TryGetTransport(string sessionId, [NotNullWhen(true)] out IMcpTransport? transport);
        public bool RemoveTransport(string sessionId);
    }
}
