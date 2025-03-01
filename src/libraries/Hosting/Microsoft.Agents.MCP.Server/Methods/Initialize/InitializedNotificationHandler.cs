using Microsoft.Agents.MCP.Core.Abstractions;
using Microsoft.Agents.MCP.Core.Handlers.Contracts.ServerMethods.Initialize;
using Microsoft.Agents.MCP.Core.PayloadHandling;
using Microsoft.Agents.MCP.Core.Payloads;

namespace Microsoft.Agents.MCP.Server.Methods.Initialize;

public class InitializedNotificationHandler : McpNotificationdHandlerBase<InitializeNotificationParameters>
{
    public override string Method => McpInitializeNotification.MethodName;

    protected override Task ExecuteAsync(IMcpContext context, McpNotification<InitializeNotificationParameters> payload, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}