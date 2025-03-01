using System.Diagnostics.CodeAnalysis;
using Microsoft.Agents.MCP.Core.Payloads;

namespace Microsoft.Agents.MCP.Core.Handlers.Contracts.SharedMethods.Ping;

public class McpPingRequest : McpRequest<PingRequestParameters>
{
    public static readonly string MethodName = "ping";

    [SetsRequiredMembers]
    public McpPingRequest(PingRequestParameters parameters)
    {
        Method = MethodName;
        Parameters = parameters;
    }
}

public class PingRequestParameters
{
    public static readonly PingRequestParameters Instance = new();
}