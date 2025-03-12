using System.Text.Json;

namespace Microsoft.Agents.Mcp.Server.Methods.Tools.ToolsCall;

public class ToolsCallResult
{
    public required bool IsError { get; init; }

    public required object? Content { get; init; }
}