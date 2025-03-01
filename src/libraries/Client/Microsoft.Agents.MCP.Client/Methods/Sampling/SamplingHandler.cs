using Microsoft.Agents.MCP.Core.Abstractions;
using Microsoft.Agents.MCP.Core.Handlers.Contracts.ClientMethods.Sampling;
using Microsoft.Agents.MCP.Core.PayloadHandling;
using Microsoft.Agents.MCP.Core.Payloads;

namespace Microsoft.Agents.MCP.Client.Methods.Sampling;

public class SamplingHandler : McpMethodPayloadHandlerBase<SamplingParameters, SamplingResult>
{
    public override string Method => McpSamplingRequest.MethodName;

    protected override Task<SamplingResult> ExecuteMethodAsync(IMcpContext context, McpRequest<SamplingParameters> payload, CancellationToken ct)
    {
        return Task.FromResult(new SamplingResult());
    }
}