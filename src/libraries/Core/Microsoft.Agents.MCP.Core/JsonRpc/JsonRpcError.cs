namespace Microsoft.Agents.MCP.Core.JsonRpc;

public class JsonRpcError
{
    public int Code { get; init; }

    public string? Message { get; init; }
}