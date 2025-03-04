using Microsoft.Agents.Mcp.Core.Abstractions;
using Microsoft.Agents.Mcp.Core.Handlers.Contracts.ClientMethods.Roots;
using Microsoft.Agents.Mcp.Core.PayloadHandling;
using Microsoft.Agents.Mcp.Core.Payloads;

namespace Microsoft.Agents.Mcp.Client.Methods.Roots;

public class RootsHandler : McpMethodWithoutInputsPayloadHandlerBase<RootsResult>
{
    public override string Method => McpRootsRequest.MethodName;

    protected override Task<RootsResult> ExecuteMethodAsync(IMcpContext context, McpRequest payload, CancellationToken ct)
    {
        return Task.FromResult(RootsResult.Instance);
    }
}