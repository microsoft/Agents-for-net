using Microsoft.Agents.Mcp.Core.Abstractions;
using Microsoft.Agents.Mcp.Core.Handlers.Contracts.ServerMethods.Initialize;
using Microsoft.Agents.Mcp.Core.PayloadHandling;
using Microsoft.Agents.Mcp.Core.Payloads;

namespace Microsoft.Agents.Mcp.Server.Methods.Initialize;

public class InitializeHandler : McpMethodPayloadHandlerBase<InitializationParameters, InitializationResult>
{
    public override string Method => McpInitializeRequest.MethodName;

    protected override Task<InitializationResult> ExecuteMethodAsync(IMcpContext context, McpRequest<InitializationParameters> payload, CancellationToken ct)
    {
        return Task.FromResult(new InitializationResult() { SessionInfo = new SessionInfo() { Id = context.SessionId } });
    }
}