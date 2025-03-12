using Microsoft.Agents.Mcp.Core.Abstractions;
using Microsoft.Agents.Mcp.Core.Handlers.Contracts.ClientMethods.Sampling;
using Microsoft.Agents.Mcp.Core.PayloadHandling;
using Microsoft.Agents.Mcp.Core.Payloads;

namespace Microsoft.Agents.Mcp.Client.Methods.Sampling;

public class SamplingHandler : McpMethodPayloadHandlerBase<SamplingParameters, SamplingResult>
{
    public override string Method => McpSamplingRequest.MethodName;

    protected override Task<SamplingResult> ExecuteMethodAsync(IMcpContext context, McpRequest<SamplingParameters> payload, CancellationToken ct)
    {
        return Task.FromResult(new SamplingResult());
    }
}