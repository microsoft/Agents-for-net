using System.Diagnostics.CodeAnalysis;
using Microsoft.Agents.MCP.Core.Payloads;

namespace Microsoft.Agents.MCP.Core.Handlers.Contracts.ServerMethods.SetLogLevel;

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