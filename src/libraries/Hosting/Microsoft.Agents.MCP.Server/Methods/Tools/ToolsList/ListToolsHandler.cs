using Microsoft.Agents.MCP.Core.Abstractions;
using Microsoft.Agents.MCP.Core.PayloadHandling;
using Microsoft.Agents.MCP.Core.Payloads;
using Microsoft.Agents.MCP.Server.Methods.Tools;

namespace Microsoft.Agents.MCP.Server.Methods.Tools.ToolsList;

public class ListToolsHandler : McpMethodPayloadHandlerBase<McpToolListRequest, ToolsListResult>
{
    public override string Method => "tools/list";

    private readonly IOperationExecutorFactory operationExecutorFactory;

    public ListToolsHandler(IOperationExecutorFactory operationExecutorFactory)
    {
        this.operationExecutorFactory = operationExecutorFactory;
    }

    protected override Task<ToolsListResult> ExecuteMethodAsync(IMcpContext context, McpRequest<McpToolListRequest> payload, CancellationToken ct)
    {
        return Task.FromResult(new ToolsListResult()
        { 
            Tools = operationExecutorFactory.GetDefinitions()
        });
    }
}