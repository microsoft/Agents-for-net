using Microsoft.Agents.Mcp.Core.Abstractions;
using Microsoft.Agents.Mcp.Core.Handlers.Contracts.ClientMethods.Logging;
using Microsoft.Agents.Mcp.Core.PayloadHandling;
using Microsoft.Agents.Mcp.Core.Payloads;

namespace Microsoft.Agents.Mcp.Client.Methods.Logging;

public class LogNotificationHandler : McpNotificationdHandlerBase<NotificationParameters>
{
    public override string Method => McpLogNotification.MethodName;

    protected override Task ExecuteAsync(IMcpContext context, McpNotification<NotificationParameters> payload, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}