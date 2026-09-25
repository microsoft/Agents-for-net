#nullable enable

using System.Collections.Generic;
using System.Text.Json;

internal sealed record ToolCallParameter(string Name, JsonElement Value);

internal sealed record ToolCallDetails(
    string Id,
    string Name,
    string? DisplayName,
    string? Category,
    string Status,
    IReadOnlyList<ToolCallParameter> FilledParameters,
    IReadOnlyList<string> UnfilledParameters,
    long? DurationMs);
