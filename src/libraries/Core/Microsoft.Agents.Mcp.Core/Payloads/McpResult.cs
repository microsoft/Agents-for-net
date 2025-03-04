using System.Text.Json.Serialization;

namespace Microsoft.Agents.Mcp.Core.Payloads;

public abstract class McpResult : McpPayload
{
    public string? Id { get; init; }

    public abstract object? Result { get; }

    public static McpResult<T> Create<T>(string id, T value)
    {
        return new McpResult<T>() { Id = id, TypedResult = value };
    }
}

public class McpResult<ResultType> : McpResult
{
    public required ResultType TypedResult { get; init; }

    public override object? Result => TypedResult;
}