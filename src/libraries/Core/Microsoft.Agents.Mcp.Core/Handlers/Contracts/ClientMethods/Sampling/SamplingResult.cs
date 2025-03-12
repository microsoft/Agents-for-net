using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Mcp.Core.Handlers.Contracts.ClientMethods.Sampling;


public class SamplingResult
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public JsonElement Content { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("stopReason")]
    public string? StopReason { get; set; }
}