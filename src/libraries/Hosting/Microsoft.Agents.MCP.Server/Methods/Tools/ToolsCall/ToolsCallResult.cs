using System.Text.Json;

namespace Microsoft.Agents.MCP.Server.Methods.Tools.ToolsCall;

public class ToolsCallResult
{
    public required bool IsError { get; init; }

    public required object? Content { get; init; }
}