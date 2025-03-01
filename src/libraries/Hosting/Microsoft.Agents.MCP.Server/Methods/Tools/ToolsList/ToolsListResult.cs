using System.Collections.Immutable;

namespace Microsoft.Agents.MCP.Server.Methods.Tools.ToolsList;

public class ToolsListResult
{
    public required ImmutableArray<ToolDefinition> Tools { get; init; }
}