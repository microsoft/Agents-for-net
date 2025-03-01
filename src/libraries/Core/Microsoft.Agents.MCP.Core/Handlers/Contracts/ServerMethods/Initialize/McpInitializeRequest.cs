using System.Diagnostics.CodeAnalysis;
using Microsoft.Agents.MCP.Core.Payloads;

namespace Microsoft.Agents.MCP.Core.Handlers.Contracts.ServerMethods.Initialize;

public class McpInitializeRequest : McpRequest<InitializationParameters>
{
    public static readonly string MethodName = "initialize";
    [SetsRequiredMembers]
    public McpInitializeRequest(InitializationParameters parameters)
    {
        Method = MethodName;
        Parameters = parameters;
    }
}