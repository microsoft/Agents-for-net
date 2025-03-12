using Microsoft.Agents.Mcp.Core.Abstractions;
using Microsoft.Agents.Mcp.Core.JsonRpc;
using Microsoft.Agents.Mcp.Core.Payloads;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Mcp.Core.PayloadHandling;


public abstract class McpMethodWithoutInputsPayloadHandlerBase<ResponseType> : McpPayloadHandlerBase<McpRequest>
{
    public override McpPayload? CreatePayload(string? id, string method, JsonElement? parameters)
    {
        return new McpRequest<JsonElement?>() { Method = method, Id = id ?? throw new ArgumentNullException(nameof(id)), Parameters = parameters };
    }

    protected abstract Task<ResponseType> ExecuteMethodAsync(IMcpContext context, McpRequest payload, CancellationToken ct);
    protected override async Task ExecuteAsync(IMcpContext context, McpRequest payload, CancellationToken ct)
    {
        var result = await ExecuteMethodAsync(context, payload, ct);
        await context.PostResultAsync(McpResult.Create(payload.Id ?? throw new ArgumentNullException(nameof(payload.Id)), result), ct);
    }
}

public abstract class McpMethodPayloadHandlerBase<RequestType> : McpPayloadHandlerBase<McpRequest<RequestType>>
{
    private static readonly JsonSerializerOptions _options = Serialization.GetDefaultMcpSerializationOptions();

    public override McpPayload? CreatePayload(string? id, string method, JsonElement? parameters)
    {
        var json = parameters?.GetRawText() ?? throw new ArgumentNullException(nameof(parameters));
        var state = JsonSerializer.Deserialize<RequestType>(json, _options);
        return new McpRequest<RequestType>() { Method = method, Id = id ?? throw new ArgumentNullException(nameof(id)), Parameters = state ?? throw new ArgumentNullException(nameof(state)) };
    }
}

public abstract class McpNotificationdHandlerBase<RequestType> : McpPayloadHandlerBase<McpNotification<RequestType>>
{
    private static readonly JsonSerializerOptions _options = Serialization.GetDefaultMcpSerializationOptions();

    public override McpPayload? CreatePayload(string? id, string method, JsonElement? parameters)
    {
        var json = parameters?.GetRawText() ?? throw new ArgumentNullException(nameof(parameters));
        var state = JsonSerializer.Deserialize<RequestType>(json, _options);
        return new McpNotification<RequestType>() { Method = method, Parameters = state ?? throw new ArgumentNullException(nameof(state)) };
    }
}

public abstract class McpMethodPayloadHandlerBase<RequestType, ResponseType> : McpPayloadHandlerBase<McpRequest<RequestType>>
{
    private static readonly JsonSerializerOptions _options = Serialization.GetDefaultMcpSerializationOptions();

    public override McpPayload? CreatePayload(string? id, string method, JsonElement? parameters)
    {
        var json = parameters?.GetRawText() ?? throw new ArgumentNullException(nameof(parameters));
        var state = JsonSerializer.Deserialize<RequestType>(json, _options);
        return new McpRequest<RequestType>() { Method = method, Id = id ?? throw new ArgumentNullException(nameof(id)), Parameters = state ?? throw new ArgumentNullException(nameof(state)) };
    }

    protected override async Task ExecuteAsync(IMcpContext context, McpRequest<RequestType> payload, CancellationToken ct)
    {
        var result = await ExecuteMethodAsync(context, payload, ct);
        await context.PostResultAsync(McpResult.Create(payload.Id, result), ct);
    }

    protected abstract Task<ResponseType> ExecuteMethodAsync(IMcpContext context, McpRequest<RequestType> payload, CancellationToken ct);
}

public abstract class McpPayloadHandlerBase : IMcpPayloadHandler
{
    public abstract string Method { get; }

    public abstract McpPayload? CreatePayload(string? id, string method, JsonElement? parameters);
    public abstract Task ExecuteAsync(IMcpContext context, McpPayload payload, CancellationToken ct);
}

public abstract class McpPayloadHandlerBase<T> : McpPayloadHandlerBase where T : McpPayload
{
    public override async Task ExecuteAsync(IMcpContext context, McpPayload payload, CancellationToken ct)
    {
        if (payload is not T typedPayload)
        {
            throw new ArgumentException($"Payload {payload.GetType().Name} is not of the expected type {typeof(T)}");
        }

        await ExecuteAsync(context, typedPayload, ct);
    }

    protected abstract Task ExecuteAsync(IMcpContext context, T payload, CancellationToken ct);
}