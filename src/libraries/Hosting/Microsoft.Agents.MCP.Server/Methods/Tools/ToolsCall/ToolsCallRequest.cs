using System.Text.Json;

namespace Microsoft.Agents.MCP.Server.Methods.Tools.ToolsCall;

public class ToolsCallRequest
{
    public required string Name { get; init; }

    public required JsonElement Arguments { get; init; }
}