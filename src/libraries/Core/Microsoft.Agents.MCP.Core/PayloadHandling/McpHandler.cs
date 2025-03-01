using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using Microsoft.Agents.MCP.Core.Payloads;
using Microsoft.Agents.MCP.Core.Abstractions;

namespace Microsoft.Agents.MCP.Core.PayloadHandling;

public class McpHandler : IMcpHandler
{
    private readonly ConditionalWeakTable<IMcpSession, ConcurrentDictionary<string, CancellationTokenSource>> methodCancellationTrackers = new();
    private readonly ILogger<McpHandler> logger;
    private readonly IMcpPayloadExecutorFactory mcpPayloadExecutorFactory;

    public McpHandler(
        ILogger<McpHandler> logger,
        IMcpPayloadExecutorFactory mcpPayloadExecutorFactory)
    {
        this.logger = logger;
        this.mcpPayloadExecutorFactory = mcpPayloadExecutorFactory;
    }

    public Task HandleAsync(IMcpSession session, McpPayload payload, CancellationToken ct)
    {
        var tokenSource = methodCancellationTrackers.GetOrCreateValue(session);
        var context = new OperationContext(session, tokenSource,logger);
        return payload switch
        {
            McpRequest methodPayload => ExecuteMethod(tokenSource, payload, context, methodPayload, ct),
            McpNotification notificationPayload => mcpPayloadExecutorFactory.GetNotificationExecutor(notificationPayload.Method).ExecuteAsync(context, payload, ct),
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

            await mcpPayloadExecutorFactory.GetMethodExecutor(methodPayload.Method).ExecuteAsync(context, payload, usedSource.Token);
        }
        finally
        {
            tokenSources.Remove(methodPayload.Id, out _);
        }
        
    }

    private Task HandleUnknownAsync(McpPayload payload)
    {
        logger.LogError("Handler processed unknown MCP payload {type}", payload.GetType().Name);
        return Task.CompletedTask;
    }

    private Task HandleErrorAsync(McpError error)
    {
        logger.LogWarning("Received MCP Error");
        return Task.CompletedTask;
    }
}