using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Mcp.Core.JsonRpc;

public class CallbackJsonRpcPayload : JsonRpcPayload
{
    [JsonPropertyName("callbackUrl")]
    public required string CallbackUrl { get; init; }

}