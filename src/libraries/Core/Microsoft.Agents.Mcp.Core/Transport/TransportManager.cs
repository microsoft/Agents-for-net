using Microsoft.Agents.Mcp.Core.Abstractions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Agents.Mcp.Core.Transport
{
    public class TransportManager : ITransportManager
    {
        private readonly ConcurrentDictionary<string, IMcpTransport> _transports = new();
        private readonly ILogger<TransportManager> _logger;

        public TransportManager(ILogger<TransportManager> logger)
        {
            _logger = logger;
        }

        public bool AddTransport(string sessionId, IMcpTransport transport)
        {
            _logger.LogInformation("Adding transport for session {SessionId}", sessionId);
            var result = _transports.TryAdd(sessionId, transport);
            if (result)
            {
                _logger.LogInformation("Transport added for session {SessionId}", sessionId);
            }
            else
            {
                _logger.LogWarning("Failed to add transport for session {SessionId}", sessionId);
            }
            return result;
        }

        public bool TryGetTransport(string sessionId, [NotNullWhen(true)] out IMcpTransport? transport)
        {
            _logger.LogInformation("Trying to get transport for session {SessionId}", sessionId);
            if (!_transports.TryGetValue(sessionId, out transport))
            {
                _logger.LogWarning("Transport not found for session {SessionId}", sessionId);
                return false;
            }

            if (transport.IsClosed)
            {
                _logger.LogInformation("Transport for session {SessionId} is closed, removing it", sessionId);
                _transports.Remove(sessionId, out _);
                return false;
            }

            _logger.LogInformation("Transport found for session {SessionId}", sessionId);
            return true;
        }

        public bool RemoveTransport(string sessionId)
        {
            _logger.LogInformation("Removing transport for session {SessionId}", sessionId);
            var result = _transports.Remove(sessionId, out _);
            if (result)
            {
                _logger.LogInformation("Transport removed for session {SessionId}", sessionId);
            }
            else
            {
                _logger.LogWarning("Failed to remove transport for session {SessionId}", sessionId);
            }
            return result;
        }
    }
}
