using Microsoft.Agents.MCP.Core.Abstractions;
using Microsoft.Agents.MCP.Core.Handlers.Contracts.SharedMethods.Cancellation;
using Microsoft.Agents.MCP.Core.PayloadHandling;
using Microsoft.Agents.MCP.Core.Payloads;

namespace Microsoft.Agents.MCP.Core.Handlers.SharedHandlers.Cancellation;

public class CancellationNotificationHandler : McpNotificationdHandlerBase<CancellationNotificationParameters>
{
    public override string Method => McpCancellationNotification.MethodName;

    protected override async Task ExecuteAsync(IMcpContext context, McpNotification<CancellationNotificationParameters> payload, CancellationToken ct)
    {
        await context.CancelOperationAsync(payload.Parameters.RequestId, ct);
    }
}