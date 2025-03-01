using Microsoft.Agents.MCP.Core.Abstractions;
using Microsoft.Agents.MCP.Core.Payloads;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Microsoft.Agents.MCP.Core.PayloadHandling;

public class OperationContext : IMcpContext
{
    private readonly IMcpSession session;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> activeOperations;
    private readonly ILogger logger;

    public string SessionId => session.SessionId;

    public OperationContext(IMcpSession session, ConcurrentDictionary<string, CancellationTokenSource> activeOperations, ILogger logger)
    {
        this.session = session;
        this.activeOperations = activeOperations;
        this.logger = logger;
    }

    public async Task CancelOperationAsync(string operationId, CancellationToken ct)
    {
        if (activeOperations.TryGetValue(operationId, out var source))
        {
            try
            {
                source.Cancel();
                logger.LogInformation($"Operation {operationId} cancelled.");
            }
            catch (Exception ex)
            {
                logger.LogError($"Error cancelling operation {operationId}.", ex);
            }
        }
        else
        {
            logger.LogWarning($"Operation {operationId} not found.");
        }

        await Task.CompletedTask;
    }

    public McpContextProperties GetContextProperties()
    {
        logger.LogDebug("Getting context properties.");
        return session.GetContextProperties();
    }

    public async Task ApplyPropertyChangesAsync(Func<McpContextProperties, McpContextProperties> apply, CancellationToken ct)
    {
        logger.LogDebug("Applying property changes.");
        await session.ApplyPropertyChangesAsync(apply, ct);
    }

    public async Task PostNotificationAsync(McpNotification payload, CancellationToken ct)
    {
        logger.LogInformation($"Posting notification: {payload.Method}");
        await session.WriteOutgoingPayload(payload, ct);
    }

    public async Task<McpPayload> PostRequestAsync(McpRequest payload, CancellationToken ct)
    {
        logger.LogInformation($"Posting request: {payload.Method}");
        var sessionReader = session.GetIncomingSessionStream(ct).GetAsyncEnumerator();
        await session.WriteOutgoingPayload(payload, ct);
        var waitForResultTask = WaitForResultAsync(payload.Id, sessionReader, ct);
        return await waitForResultTask;
    }

    private async Task<McpPayload> WaitForResultAsync(string id, IAsyncEnumerator<McpPayload> sessionReader, CancellationToken ct)
    {
        logger.LogDebug($"Waiting for result with ID: {id}");
        try
        {
            while (await sessionReader.MoveNextAsync())
            {
                var incomingPayload = sessionReader.Current;
                if (incomingPayload is McpResult result && result.Id == id)
                {
                    logger.LogInformation($"Received result for ID: {id}");
                    return result;
                }
                if (incomingPayload is McpError error && error.Id == id)
                {
                    logger.LogError($"Received error for ID: {id}");
                    return error;
                }
            }
        }
        finally
        {
            await sessionReader.DisposeAsync();
        }


        var errorMessage = "Unable to get result for a closed session";
        logger.LogError(errorMessage);
        throw new ArgumentException(errorMessage);
    }

    public async Task PostResultAsync(McpResult payload, CancellationToken ct)
    {
        logger.LogInformation($"Posting result for ID: {payload.Id}");
        await session.WriteOutgoingPayload(payload, ct);
    }

    public async Task PostErrorAsync(McpError payload, CancellationToken ct)
    {
        logger.LogError($"Posting error for ID: {payload.Id}");
        await session.WriteOutgoingPayload(payload, ct);
    }
}
