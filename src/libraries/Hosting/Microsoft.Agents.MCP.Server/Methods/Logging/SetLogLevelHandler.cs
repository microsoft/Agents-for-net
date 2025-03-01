using Microsoft.Agents.MCP.Core.Abstractions;
using Microsoft.Agents.MCP.Core.Handlers.Contracts.ServerMethods.SetLogLevel;
using Microsoft.Agents.MCP.Core.PayloadHandling;
using Microsoft.Agents.MCP.Core.Payloads;

namespace Microsoft.Agents.MCP.Server.Methods.Logging;

public class SetLogLevelHandler : McpMethodPayloadHandlerBase<SetLogLevelParameters, SetLogLevelResult>
{
    public override string Method => McpSetLogLevelRequest.MethodName;

    protected override async Task<SetLogLevelResult> ExecuteMethodAsync(IMcpContext context, McpRequest<SetLogLevelParameters> payload, CancellationToken ct)
    {
        await context.ApplyPropertyChangesAsync((properties) => properties with { LogLevel = payload.Parameters.Level }, ct);
        return SetLogLevelResult.Instance;
    }
}