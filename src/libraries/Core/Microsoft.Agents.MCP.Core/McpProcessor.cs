using Microsoft.Extensions.Logging;
using Microsoft.Agents.MCP.Core.Payloads;
using Microsoft.Agents.MCP.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.MCP.Core;

public class McpProcessor : IMcpProcessor
{
    private readonly ILogger<McpProcessor> logger;
    private readonly IMcpHandler handler;
    private readonly IMcpSessionManager sessionManager;

    public McpProcessor(
        ILogger<McpProcessor> logger,
        IMcpHandler handler,
        IMcpSessionManager sessionManager)
    {
        this.logger = logger;
        this.handler = handler;
        this.sessionManager = sessionManager;
    }

    public async Task<IMcpSession> CreateSessionAsync(IMcpTransport transport, CancellationToken ct)
    {
        var session = await sessionManager.CreateSessionAsync(transport, ct);
        // Hook session into processing pipeline
        _ = Task.Run(() => ConnectSessionAsync(session, ct));
        return session;
    }

    private async Task ConnectSessionAsync(IMcpSession session, CancellationToken ct)
    {
        using var sharedToken = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            await foreach (var incomingPayload in session.GetIncomingSessionStream(ct))
            {
                _ = Task.Run(() => ExecuteOperation(session, incomingPayload, sharedToken.Token)); 
            }
        }
        catch (Exception)
        {
            sharedToken.Cancel();
        }

    }

    private async Task ExecuteOperation(IMcpSession session, McpPayload incomingPayload, CancellationToken ct)
    {
        try
        {
            await handler.HandleAsync(session, incomingPayload, ct);
        }
        catch (Exception)
        {
            logger.LogError("Error handling payload {incomingPayload}", incomingPayload);
        }
    }
}