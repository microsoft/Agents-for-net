using System.Collections.Immutable;

namespace Microsoft.Agents.Mcp.Server.Methods.Tools.ToolsList;

public class ToolsListResult
{
    public required ImmutableArray<ToolDefinition> Tools { get; init; }
}