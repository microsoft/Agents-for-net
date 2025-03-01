using Microsoft.Agents.MCP.Core.Abstractions;
using Microsoft.Agents.MCP.Core.JsonRpc;
using Microsoft.Agents.MCP.Core.Payloads;
using Microsoft.Agents.MCP.Server.Methods.Tools.ToolsCall;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.MCP.Server.Methods.Tools.ToolsCall.Handlers;

public abstract class McpToolExecutorBase : IMcpToolExecutor
{
    public abstract Type InputType { get; }
    public abstract string Id { get; }
    public abstract string Description { get; }
    public abstract Task<ToolsCallResult> ExecuteAsync(McpRequest<ToolsCallRequest> payload, IMcpContext context, CancellationToken ct);
}

public abstract class McpToolExecutorBase<InputSchema, OutputSchema> : McpToolExecutorBase
{
    private static readonly JsonSerializerOptions _options = Serialization.GetDefaultMcpSerializationOptions();

    public override Type InputType => typeof(InputSchema);

    public abstract Task<OutputSchema> ExecuteAsync(McpRequest<InputSchema> payload, IMcpContext context, CancellationToken ct);

    public override async Task<ToolsCallResult> ExecuteAsync(McpRequest<ToolsCallRequest> payload, IMcpContext context, CancellationToken ct)
    {
        var json = payload.Parameters.Arguments.GetRawText();
        var state = JsonSerializer.Deserialize<InputSchema>(json, _options) ?? throw new ArgumentNullException(nameof(payload.Parameters.Arguments));
        var typedPayload = McpRequest.CreateFrom(payload, state);

        var result = await ExecuteAsync(typedPayload, context, ct);
        return new ToolsCallResult() { Content = result, IsError = false };
    }
}
