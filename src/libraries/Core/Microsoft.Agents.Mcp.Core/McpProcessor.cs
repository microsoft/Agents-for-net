using Microsoft.Extensions.Logging;
using Microsoft.Agents.Mcp.Core.Payloads;
using Microsoft.Agents.Mcp.Core.Abstractions;

namespace Microsoft.Agents.Mcp.Core;

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
        var enumerator = session.GetIncomingSessionStream(CancellationToken.None).GetAsyncEnumerator();
        
        // Hook session into processing pipeline
        _ = Task.Run(() => ConnectSessionAsync(session, enumerator, ct));
        return session;
    }

    private async Task ConnectSessionAsync(IMcpSession session, IAsyncEnumerator<McpPayload> enumerator, CancellationToken ct)
    {
        using var sharedToken = new CancellationTokenSource();

        try
        {
            while (await enumerator.MoveNextAsync())
            {
                _ = Task.Run(() => ExecuteOperation(session, enumerator.Current, sharedToken.Token)); 
            }
        }
        catch (Exception)
        {
            sharedToken.Cancel();
        }
        finally
        {
            sharedToken.Dispose();
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