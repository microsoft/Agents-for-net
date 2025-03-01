using Microsoft.Agents.MCP.Core.Abstractions;
using Microsoft.Agents.MCP.Core.Handlers.Contracts.SharedMethods.Ping;
using Microsoft.Agents.MCP.Core.PayloadHandling;
using Microsoft.Agents.MCP.Core.Payloads;

namespace Microsoft.Agents.MCP.Core.Handlers.SharedHandlers.Ping;

public class PingHandler : McpMethodWithoutInputsPayloadHandlerBase<PingResponse>
{
    public override string Method => McpPingRequest.MethodName;

    protected override Task<PingResponse> ExecuteMethodAsync(IMcpContext context, McpRequest payload, CancellationToken ct)
    {
        return Task.FromResult(PingResponse.Instance);
    }
}