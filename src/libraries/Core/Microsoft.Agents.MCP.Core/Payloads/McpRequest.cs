namespace Microsoft.Agents.MCP.Core.Payloads;

public abstract class McpRequest : McpPayload
{
    public required string Method { get; init; }

    public abstract object? Params { get; }
    public string Id { get; init; } = Guid.NewGuid().ToString();

    public static McpRequest<T> CreateFrom<T>(McpRequest payload, T state)
    {
        return new McpRequest<T>()
        {
            Method = payload.Method,
            Parameters = state,
            Id = payload.Id
        };
    }
}

public class McpRequest<RequestType> : McpRequest
{
    public required RequestType Parameters { get; init; }

    public override object? Params => Parameters;
}