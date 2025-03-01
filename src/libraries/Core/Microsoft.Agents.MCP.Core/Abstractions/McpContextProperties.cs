using System.Collections.Immutable;

namespace Microsoft.Agents.MCP.Core.Abstractions;

public record McpContextProperties
{
    public string? LogLevel { get; init; }

    public ImmutableDictionary<string, object> PropertyBag { get; init; } = ImmutableDictionary<string, object>.Empty;
}