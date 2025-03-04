using System.Text.Json;
using Microsoft.Agents.Mcp.Core.Abstractions;
using Microsoft.Agents.Mcp.Core.Payloads;

namespace Microsoft.Agents.Mcp.Server.Methods.Tools.ToolsCall.Handlers;

public interface IMcpToolExecutor
{
    public abstract Type InputType { get; }
    public abstract string Id { get; }
    public abstract string Description { get; }
    public abstract Task<ToolsCallResult> ExecuteAsync(McpRequest<ToolsCallRequest> payload, IMcpContext context, CancellationToken ct);
}