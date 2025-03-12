using System.Diagnostics.CodeAnalysis;
using Microsoft.Agents.Mcp.Core.Payloads;

namespace Microsoft.Agents.Mcp.Core.Handlers.Contracts.ServerMethods.SetLogLevel;

public class McpSetLogLevelRequest : McpRequest<SetLogLevelParameters>
{
    public static readonly string MethodName = "logging/setLevel";
    [SetsRequiredMembers]
    public McpSetLogLevelRequest(SetLogLevelParameters parameters)
    {
        Method = "sampling/createMessage";
        Parameters = parameters;
    }
}