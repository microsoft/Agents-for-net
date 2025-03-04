namespace Microsoft.Agents.Mcp.Core.JsonRpc;

public class JsonRpcError
{
    public int Code { get; init; }

    public string? Message { get; init; }
}