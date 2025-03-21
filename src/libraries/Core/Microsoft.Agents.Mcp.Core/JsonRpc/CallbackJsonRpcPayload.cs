using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Mcp.Core.JsonRpc;

public class CallbackJsonRpcPayload : JsonRpcPayload
{
    [JsonPropertyName("callbackEndpoint")]
    public required string CallbackUrl { get; init; }

}