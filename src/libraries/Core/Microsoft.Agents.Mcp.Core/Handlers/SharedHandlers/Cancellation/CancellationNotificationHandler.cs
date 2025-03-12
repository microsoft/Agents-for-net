using Microsoft.Agents.Mcp.Core.Abstractions;
using Microsoft.Agents.Mcp.Core.Handlers.Contracts.SharedMethods.Cancellation;
using Microsoft.Agents.Mcp.Core.PayloadHandling;
using Microsoft.Agents.Mcp.Core.Payloads;

namespace Microsoft.Agents.Mcp.Core.Handlers.SharedHandlers.Cancellation;

public class CancellationNotificationHandler : McpNotificationdHandlerBase<CancellationNotificationParameters>
{
    public override string Method => McpCancellationNotification.MethodName;

    protected override async Task ExecuteAsync(IMcpContext context, McpNotification<CancellationNotificationParameters> payload, CancellationToken ct)
    {
        await context.CancelOperationAsync(payload.Parameters.RequestId, ct);
    }
}