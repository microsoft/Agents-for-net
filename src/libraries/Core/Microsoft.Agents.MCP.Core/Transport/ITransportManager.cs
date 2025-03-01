using System.Diagnostics.CodeAnalysis;
using Microsoft.Agents.MCP.Core.Abstractions;

namespace Microsoft.Agents.MCP.Core.Transport
{
    public interface ITransportManager
    {
        public bool AddTransport(string sessionId, IMcpTransport transport);
        public bool TryGetTransport(string sessionId, [NotNullWhen(true)] out IMcpTransport? transport);
        public bool RemoveTransport(string sessionId);
    }
}
