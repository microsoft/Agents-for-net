using System.Diagnostics.CodeAnalysis;
using Microsoft.Agents.MCP.Core.Payloads;

namespace Microsoft.Agents.MCP.Core.Handlers.Contracts.SharedMethods.Cancellation;

public class McpCancellationNotification : McpNotification<CancellationNotificationParameters>
{
    public static readonly string MethodName = "notifications/cancelled";

    [SetsRequiredMembers]
    public McpCancellationNotification(CancellationNotificationParameters parameters)
    {
        Method = MethodName;
        Parameters = parameters;
    }
}
