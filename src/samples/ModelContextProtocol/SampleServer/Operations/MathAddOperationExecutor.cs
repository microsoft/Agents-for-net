
using Microsoft.Agents.MCP.Core.Abstractions;
using Microsoft.Agents.MCP.Core.Handlers.Contracts.ClientMethods.Logging;
using Microsoft.Agents.MCP.Core.Payloads;
using Microsoft.Agents.MCP.Server.Methods.Tools.ToolsCall.Handlers;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.MCP.Server.Sample.Operations;

public struct MathAddInput
{
    [Description("the first referenced number")]
    public required string Number1 { get; init; }

    [Description("the second referenced number")]
    public required string Number2 { get; init; }
}

public struct MathAddOutput
{
    public required int Total { get; init; }
}

public class MathAddOperationExecutor : McpToolExecutorBase<MathAddInput, MathAddOutput>
{
    public override string Id => "Math_Adder";

    public override string Description => "Adds two numbers";

    public override async Task<MathAddOutput> ExecuteAsync(McpRequest<MathAddInput> payload, IMcpContext context, CancellationToken ct)
    {
        var n1 = int.Parse(payload.Parameters.Number1);
        var n2 = int.Parse(payload.Parameters.Number2);
        var result = new MathAddOutput() { Total = n1 + n2 };

        await context.PostNotificationAsync(new McpLogNotification<string>(
             new NotificationParameters<string>()
             {
                 Level = "notice",
                 Logger = "echo",
                 Data = $"Adding {n1} and {n2}"
             }), ct);

        return result;
    }
}
