using Microsoft.Agents.MCP.Core.Abstractions;
using Microsoft.Agents.MCP.Core.Handlers.Contracts.ClientMethods.Roots;
using Microsoft.Agents.MCP.Core.PayloadHandling;
using Microsoft.Agents.MCP.Core.Payloads;

namespace Microsoft.Agents.MCP.Client.Methods.Roots;

public class RootsHandler : McpMethodWithoutInputsPayloadHandlerBase<RootsResult>
{
    public override string Method => McpRootsRequest.MethodName;

    protected override Task<RootsResult> ExecuteMethodAsync(IMcpContext context, McpRequest payload, CancellationToken ct)
    {
        return Task.FromResult(RootsResult.Instance);
    }
}