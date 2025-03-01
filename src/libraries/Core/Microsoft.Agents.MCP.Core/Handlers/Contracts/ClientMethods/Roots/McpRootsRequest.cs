using System.Diagnostics.CodeAnalysis;
using Microsoft.Agents.MCP.Core.Payloads;

namespace Microsoft.Agents.MCP.Core.Handlers.Contracts.ClientMethods.Roots;

public class McpRootsRequest : McpRequest
{
    public static readonly string MethodName = "roots/list";

    [SetsRequiredMembers]
    public McpRootsRequest()
    {
        Method = MethodName;
    }

    public override object? Params => null;
}