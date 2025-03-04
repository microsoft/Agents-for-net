using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using Microsoft.Agents.Mcp.Core.Payloads;
using Microsoft.Agents.Mcp.Core.Abstractions;

namespace Microsoft.Agents.Mcp.Core.PayloadHandling;

public class McpHandler : IMcpHandler
{
    private readonly ConditionalWeakTable<IMcpSession, ConcurrentDictionary<string, CancellationTokenSource>> methodCancellationTrackers = new();
    private readonly ILogger<McpHandler> logger;
    private readonly IMcpPayloadExecutorFactory McpPayloadExecutorFactory;

    public McpHandler(
        ILogger<McpHandler> logger,
        IMcpPayloadExecutorFactory McpPayloadExecutorFactory)
    {
        this.logger = logger;
        this.McpPayloadExecutorFactory = McpPayloadExecutorFactory;
    }

    public Task HandleAsync(IMcpSession session, McpPayload payload, CancellationToken ct)
    {
        var tokenSource = methodCancellationTrackers.GetOrCreateValue(session);
        var context = new OperationContext(session, tokenSource,logger);
        return payload switch
        {
            McpRequest methodPayload => ExecuteMethod(tokenSource, payload, context, methodPayload, ct),
            McpNotification notificationPayload => McpPayloadExecutorFactory.GetNotificationExecutor(notificationPayload.Method).ExecuteAsync(context, payload, ct),
            McpError error => HandleErrorAsync(error),
            McpResult result => Task.CompletedTask, // Results are handled by dedicated listeners
            _ => HandleUnknownAsync(payload),
        };
    }

    private async Task ExecuteMethod(
        ConcurrentDictionary<string, CancellationTokenSource> tokenSources, 
        McpPayload payload, 
        OperationContext context, 
        McpRequest methodPayload, 
        CancellationToken ct)
    {
        using var wrappedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            var usedSource = tokenSources.AddOrUpdate(methodPayload.Id, wrappedTokenSource, (id, existingSource) =>
            {
                logger.LogWarning("Multiple incoming methods with the same id. sharing cancellation source. Can cause errors");
                return existingSource;
            });

            await McpPayloadExecutorFactory.GetMethodExecutor(methodPayload.Method).ExecuteAsync(context, payload, usedSource.Token);
        }
        finally
        {
            tokenSources.Remove(methodPayload.Id, out _);
        }
        
    }

    private Task HandleUnknownAsync(McpPayload payload)
    {
        logger.LogError("Handler processed unknown Mcp payload {type}", payload.GetType().Name);
        return Task.CompletedTask;
    }

    private Task HandleErrorAsync(McpError error)
    {
        logger.LogWarning("Received Mcp Error");
        return Task.CompletedTask;
    }
}