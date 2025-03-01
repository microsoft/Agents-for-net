namespace Microsoft.Agents.MCP.Core.Payloads;

public abstract class McpNotification : McpPayload
{
    public required string Method { get; init; }

    public abstract object? Params { get; }
}

public class McpNotification<RequestType> : McpNotification
{
    public required RequestType Parameters { get; init; }
    public override object? Params => Parameters;
}