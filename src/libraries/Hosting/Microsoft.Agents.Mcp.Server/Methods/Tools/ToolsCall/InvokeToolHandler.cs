using System.Text.Json;
using Microsoft.Agents.Mcp.Core.Abstractions;
using Microsoft.Agents.Mcp.Core.PayloadHandling;
using Microsoft.Agents.Mcp.Core.Payloads;

namespace Microsoft.Agents.Mcp.Server.Methods.Tools.ToolsCall;

public class InvokeToolHandler : McpMethodPayloadHandlerBase<ToolsCallRequest, ToolsCallResult>
{
    private readonly IOperationExecutorFactory operationExecutorFactory;

    public override string Method => "tools/call";
    public InvokeToolHandler(IOperationExecutorFactory operationExecutorFactory)
    {
        this.operationExecutorFactory = operationExecutorFactory;
    }

    protected override async Task<ToolsCallResult> ExecuteMethodAsync(IMcpContext context, McpRequest<ToolsCallRequest> payload, CancellationToken ct)
    {
        var result = await operationExecutorFactory.GetExecutor(payload.Parameters.Name).ExecuteAsync(payload, context, ct);
        return result;
    }
}