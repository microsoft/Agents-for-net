using Microsoft.Agents.Mcp.Core.Abstractions;
using Microsoft.Agents.Mcp.Core.Handlers.Contracts.ServerMethods.Initialize;
using Microsoft.Agents.Mcp.Core.PayloadHandling;
using Microsoft.Agents.Mcp.Core.Payloads;

namespace Microsoft.Agents.Mcp.Server.Methods.Initialize;

public class InitializedNotificationHandler : McpNotificationdHandlerBase<InitializeNotificationParameters>
{
    public override string Method => McpInitializeNotification.MethodName;

    protected override Task ExecuteAsync(IMcpContext context, McpNotification<InitializeNotificationParameters> payload, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}