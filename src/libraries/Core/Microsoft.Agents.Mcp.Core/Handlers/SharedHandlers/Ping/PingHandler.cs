using Microsoft.Agents.Mcp.Core.Abstractions;
using Microsoft.Agents.Mcp.Core.Handlers.Contracts.SharedMethods.Ping;
using Microsoft.Agents.Mcp.Core.PayloadHandling;
using Microsoft.Agents.Mcp.Core.Payloads;

namespace Microsoft.Agents.Mcp.Core.Handlers.SharedHandlers.Ping;

public class PingHandler : McpMethodWithoutInputsPayloadHandlerBase<PingResponse>
{
    public override string Method => McpPingRequest.MethodName;

    protected override Task<PingResponse> ExecuteMethodAsync(IMcpContext context, McpRequest payload, CancellationToken ct)
    {
        return Task.FromResult(PingResponse.Instance);
    }
}