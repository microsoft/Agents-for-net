using System.Text.Json.Nodes;

namespace Microsoft.Agents.MCP.Server.Methods.Tools.ToolsList;

public class ToolDefinition
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required JsonNode InputSchema { get; set; }
}