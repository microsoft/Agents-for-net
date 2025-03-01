namespace Microsoft.Agents.MCP.Core.Handlers.Contracts.ServerMethods.Initialize;

public class InitializationResult
{
    public required SessionInfo SessionInfo { get; init; }
}

public class SessionInfo
{
    public required string Id { get; set; }
}