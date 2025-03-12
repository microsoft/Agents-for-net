using System.Diagnostics.CodeAnalysis;
using Microsoft.Agents.Mcp.Core.Payloads;

namespace Microsoft.Agents.Mcp.Core.Handlers.Contracts.SharedMethods.Ping;

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