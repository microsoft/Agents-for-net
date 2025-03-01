using Microsoft.Agents.MCP.Core.Abstractions;
using Microsoft.Agents.MCP.Core.Handlers.Contracts.ClientMethods.Logging;
using Microsoft.Agents.MCP.Core.PayloadHandling;
using Microsoft.Agents.MCP.Core.Payloads;

namespace Microsoft.Agents.MCP.Client.Methods.Logging;

public class LogNotificationHandler : McpNotificationdHandlerBase<NotificationParameters>
{
    public override string Method => McpLogNotification.MethodName;

    protected override Task ExecuteAsync(IMcpContext context, McpNotification<NotificationParameters> payload, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}