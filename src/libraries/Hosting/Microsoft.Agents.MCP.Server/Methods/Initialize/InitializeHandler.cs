using Microsoft.Agents.MCP.Core.Abstractions;
using Microsoft.Agents.MCP.Core.Handlers.Contracts.ServerMethods.Initialize;
using Microsoft.Agents.MCP.Core.PayloadHandling;
using Microsoft.Agents.MCP.Core.Payloads;

namespace Microsoft.Agents.MCP.Server.Methods.Initialize;

public class InitializeHandler : McpMethodPayloadHandlerBase<InitializationParameters, InitializationResult>
{
    public override string Method => McpInitializeRequest.MethodName;

    protected override Task<InitializationResult> ExecuteMethodAsync(IMcpContext context, McpRequest<InitializationParameters> payload, CancellationToken ct)
    {
        return Task.FromResult(new InitializationResult() { SessionInfo = new SessionInfo() { Id = context.SessionId } });
    }
}