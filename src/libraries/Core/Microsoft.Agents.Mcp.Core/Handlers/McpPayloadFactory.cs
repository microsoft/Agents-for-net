using Microsoft.Agents.Mcp.Core.JsonRpc;
using System.Text.Json;
using Microsoft.Agents.Mcp.Core.Payloads;
using Microsoft.Agents.Mcp.Core.Abstractions;

namespace Microsoft.Agents.Mcp.Core.Handlers;

public class McpPayloadFactory : IMcpPayloadFactory
{
    private static readonly JsonElement NullJsonElement = JsonDocument.Parse("null").RootElement;
    private readonly IMcpPayloadResolver payloadResolver;

    public McpPayloadFactory(IMcpPayloadResolver payloadResolver)
    {
        this.payloadResolver = payloadResolver;
    }

    public JsonRpcPayload CreateJsonRpcPayload(McpPayload McpPayload)
    {
        return McpPayload switch
        {
            McpError error => new JsonRpcPayload()
            {
                Id = error.Id,
                Error = error.Error
            },
            McpResult result => new JsonRpcPayload()
            {
                Id = result.Id,
                Result = JsonSerializer.SerializeToElement(result.Result)
            },
            McpNotification notification => new JsonRpcPayload()
            {
                Method = notification.Method,
                Params = JsonSerializer.SerializeToElement(notification.Params)
            },
            McpRequest request => new JsonRpcPayload()
            {
                Id = request.Id,
                Method = request.Method,
                Params = JsonSerializer.SerializeToElement(request.Params)
            },
            _ => throw new Exception("Unknown Mcp type"),
        };
    }

    public McpPayload CreatePayload(JsonRpcPayload jsonRpcPayload)
    {
        // Error
        if (jsonRpcPayload.Error != null)
        {
            return new McpError()
            {
                Id = jsonRpcPayload.Id,
                Error = jsonRpcPayload.Error
            };
        }

        // Notifications
        if (jsonRpcPayload.Id == null && jsonRpcPayload.Method != null)
        {
            return payloadResolver.CreateNotificationPayload(jsonRpcPayload.Method, jsonRpcPayload.Params);
        }

        // Response
        if (jsonRpcPayload.Id != null && jsonRpcPayload.Result != null)
        {
            return new McpResult<JsonElement>()
            {
                Id = jsonRpcPayload.Id,
                TypedResult = jsonRpcPayload.Result ?? NullJsonElement,
            };
        }

        // Method
        if (jsonRpcPayload.Method != null)
        {
            return payloadResolver.CreateMethodRequestPayload(jsonRpcPayload.Id, jsonRpcPayload.Method, jsonRpcPayload.Params);
        }

        throw new NotImplementedException("Unknown request format");
    }
}
