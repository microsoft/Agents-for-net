using System.Diagnostics.CodeAnalysis;
using Microsoft.Agents.MCP.Core.Payloads;

namespace Microsoft.Agents.MCP.Core.Handlers.Contracts.ServerMethods.Initialize;

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