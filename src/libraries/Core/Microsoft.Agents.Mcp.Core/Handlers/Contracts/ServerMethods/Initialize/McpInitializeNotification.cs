using System.Diagnostics.CodeAnalysis;
using Microsoft.Agents.Mcp.Core.Payloads;

namespace Microsoft.Agents.Mcp.Core.Handlers.Contracts.ServerMethods.Initialize;

public class McpInitializeNotification : McpNotification<InitializeNotificationParameters>
{
    public static readonly string MethodName = "notifications/initialized";

    [SetsRequiredMembers]
    public McpInitializeNotification(InitializeNotificationParameters parameters)
    {
        Method = MethodName;
        Parameters = parameters;
    }
}