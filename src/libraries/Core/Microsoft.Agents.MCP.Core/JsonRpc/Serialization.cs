using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.MCP.Core.JsonRpc;

public static class Serialization
{
    public static JsonSerializerOptions GetDefaultMcpSerializationOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}