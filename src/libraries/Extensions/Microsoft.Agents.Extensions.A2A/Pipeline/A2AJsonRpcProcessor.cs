using A2A;
using A2A.AspNetCore;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace Microsoft.Agents.Extensions.A2A.Pipeline;

/// <summary>
/// Static processor class for handling A2A JSON-RPC requests in ASP.NET Core applications.
/// </summary>
/// <remarks>This is a copy of the a2a-dotnet version since those are internal and can't be used directly.</remarks>
internal static class A2AJsonRpcProcessor
{
    internal static async Task<IResult> ProcessRequestAsync(
        IA2ARequestHandler requestHandler,
        HttpRequest request,
        CancellationToken cancellationToken,
        Func<JsonRpcId, string, JsonElement?, CancellationToken, Task<JsonRpcResponseResult>> extensionHandler = null)
    {
        // Version negotiation: check A2A-Version header
        var version = request.Headers["A2A-Version"].FirstOrDefault();
        if (!string.IsNullOrEmpty(version) && version != "1.0" && version != "0.3")
        {
            return new JsonRpcResponseResult(JsonRpcResponse.CreateJsonRpcErrorResponse(
                new JsonRpcId((string?)null),
                new A2AException(
                    $"Protocol version '{version}' is not supported. Supported versions: 0.3, 1.0",
                    A2AErrorCode.VersionNotSupported)));
        }

        //using var activity = A2AAspNetCoreDiagnostics.Source.StartActivity("HandleA2ARequest", ActivityKind.Server);

        JsonRpcRequest? rpcRequest = null;
        JsonRpcId? parsedRequestId = null;

        try
        {
            using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = document.RootElement;
            var requestId = GetRequestId(root);
            parsedRequestId = requestId;
            var method = root.TryGetProperty("method", out var methodElement) && methodElement.ValueKind == JsonValueKind.String
                ? methodElement.GetString()
                : null;
            var parameters = root.TryGetProperty("params", out var paramsElement)
                ? paramsElement
                : (JsonElement?)null;

            if (extensionHandler != null && !string.IsNullOrEmpty(method))
            {
                var extensionResponse = await extensionHandler(requestId, method, parameters, cancellationToken).ConfigureAwait(false);
                if (extensionResponse != null)
                {
                    return extensionResponse;
                }
            }

            rpcRequest = root.Deserialize(A2AJsonUtilities.DefaultOptions.GetTypeInfo(typeof(JsonRpcRequest))) as JsonRpcRequest;

            //activity?.SetTag("request.id", rpcRequest!.Id.ToString());
            //activity?.SetTag("request.method", rpcRequest!.Method);

            if (A2AMethods.IsStreamingMethod(rpcRequest!.Method))
            {
                return StreamResponse(requestHandler, rpcRequest.Id, rpcRequest.Method, rpcRequest.Params, cancellationToken);
            }

            return await SingleResponseAsync(requestHandler, rpcRequest.Id, rpcRequest.Method, rpcRequest.Params, cancellationToken, extensionHandler).ConfigureAwait(false);
        }
        catch (A2AException ex)
        {
            //activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            var errorId = rpcRequest?.Id ?? parsedRequestId ?? new JsonRpcId(ex.GetRequestId());
            return new JsonRpcResponseResult(JsonRpcResponse.CreateJsonRpcErrorResponse(errorId, ex));
        }
        catch (Exception)
        {
            //activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            var errorId = rpcRequest?.Id ?? parsedRequestId ?? new JsonRpcId((string?)null);
            return new JsonRpcResponseResult(JsonRpcResponse.InternalErrorResponse(errorId, "An internal error occurred."));
        }
    }

    internal static async Task<JsonRpcResponseResult> SingleResponseAsync(
        IA2ARequestHandler requestHandler,
        JsonRpcId requestId,
        string method,
        JsonElement? parameters,
        CancellationToken cancellationToken,
        Func<JsonRpcId, string, JsonElement?, CancellationToken, Task<JsonRpcResponseResult>> extensionHandler = null)
    {
        //using var activity = A2AAspNetCoreDiagnostics.Source.StartActivity($"SingleResponse/{method}", ActivityKind.Server);
        //activity?.SetTag("request.id", requestId.ToString());
        //activity?.SetTag("request.method", method);

        JsonRpcResponse? response = null;

        if (parameters == null)
        {
            //activity?.SetStatus(ActivityStatusCode.Error, "Invalid parameters");
            return new JsonRpcResponseResult(JsonRpcResponse.InvalidParamsResponse(requestId));
        }

        // For push notification methods, check if push notifications are supported
        // BEFORE deserializing params. DeserializeAndValidate would throw InvalidParams
        // for malformed requests, masking the PushNotificationNotSupported error.
        if (A2AMethods.IsPushNotificationMethod(method))
        {
            try
            {
                await requestHandler.GetTaskPushNotificationConfigAsync(null!, cancellationToken).ConfigureAwait(false);
            }
            catch (A2AException ex) when (ex.ErrorCode == A2AErrorCode.PushNotificationNotSupported)
            {
                throw;
            }
            catch
            {
                // Any other exception means push notifications are supported;
                // continue with normal deserialization and handling.
            }
        }

        switch (method)
        {
            case A2AMethods.SendMessage:
                var sendRequest = DeserializeAndValidate<SendMessageRequest>(parameters.Value);
                var sendResult = await requestHandler.SendMessageAsync(sendRequest, cancellationToken).ConfigureAwait(false);
                response = JsonRpcResponse.CreateJsonRpcResponse(requestId, sendResult);
                break;
            case A2AMethods.GetTask:
                var getTaskRequest = DeserializeAndValidate<GetTaskRequest>(parameters.Value);
                var agentTask = await requestHandler.GetTaskAsync(getTaskRequest, cancellationToken).ConfigureAwait(false);
                response = JsonRpcResponse.CreateJsonRpcResponse(requestId, agentTask);
                break;
            case A2AMethods.ListTasks:
                var listTasksRequest = DeserializeAndValidate<ListTasksRequest>(parameters.Value);

                // Validate pageSize: must be 1-100 if specified
                if (listTasksRequest.PageSize is { } ps && (ps <= 0 || ps > 100))
                {
                    throw new A2AException(
                        $"Invalid pageSize: {ps}. Must be between 1 and 100.",
                        A2AErrorCode.InvalidParams);
                }

                // Validate historyLength: must be >= 0 if specified
                if (listTasksRequest.HistoryLength is { } hl && hl < 0)
                {
                    throw new A2AException(
                        $"Invalid historyLength: {hl}. Must be non-negative.",
                        A2AErrorCode.InvalidParams);
                }

                var listResult = await requestHandler.ListTasksAsync(listTasksRequest, cancellationToken).ConfigureAwait(false);
                response = JsonRpcResponse.CreateJsonRpcResponse(requestId, listResult);
                break;
            case A2AMethods.CancelTask:
                var cancelRequest = DeserializeAndValidate<CancelTaskRequest>(parameters.Value);
                var cancelledTask = await requestHandler.CancelTaskAsync(cancelRequest, cancellationToken).ConfigureAwait(false);
                response = JsonRpcResponse.CreateJsonRpcResponse(requestId, cancelledTask);
                break;
            case A2AMethods.CreateTaskPushNotificationConfig:
                var createPnConfig = DeserializeAndValidate<CreateTaskPushNotificationConfigRequest>(parameters.Value);
                var createdConfig = await requestHandler.CreateTaskPushNotificationConfigAsync(createPnConfig, cancellationToken).ConfigureAwait(false);
                response = JsonRpcResponse.CreateJsonRpcResponse(requestId, createdConfig);
                break;
            case A2AMethods.GetTaskPushNotificationConfig:
                var getPnConfig = DeserializeAndValidate<GetTaskPushNotificationConfigRequest>(parameters.Value);
                var gotConfig = await requestHandler.GetTaskPushNotificationConfigAsync(getPnConfig, cancellationToken).ConfigureAwait(false);
                response = JsonRpcResponse.CreateJsonRpcResponse(requestId, gotConfig);
                break;
            case A2AMethods.ListTaskPushNotificationConfig:
                var listPnConfig = DeserializeAndValidate<ListTaskPushNotificationConfigRequest>(parameters.Value);
                var listPnResult = await requestHandler.ListTaskPushNotificationConfigAsync(listPnConfig, cancellationToken).ConfigureAwait(false);
                response = JsonRpcResponse.CreateJsonRpcResponse(requestId, listPnResult);
                break;
            case A2AMethods.DeleteTaskPushNotificationConfig:
                var deletePnConfig = DeserializeAndValidate<DeleteTaskPushNotificationConfigRequest>(parameters.Value);
                await requestHandler.DeleteTaskPushNotificationConfigAsync(deletePnConfig, cancellationToken).ConfigureAwait(false);
                response = JsonRpcResponse.CreateJsonRpcResponse(requestId, (object?)null);
                break;
            case A2AMethods.GetExtendedAgentCard:
                var getCardRequest = DeserializeAndValidate<GetExtendedAgentCardRequest>(parameters.Value);
                var extCard = await requestHandler.GetExtendedAgentCardAsync(getCardRequest, cancellationToken).ConfigureAwait(false);
                response = JsonRpcResponse.CreateJsonRpcResponse(requestId, extCard);
                break;
            default:
                return new JsonRpcResponseResult(JsonRpcResponse.MethodNotFoundResponse(requestId));
        }

        return new JsonRpcResponseResult(response);
    }

    private static JsonRpcId GetRequestId(JsonElement request)
    {
        if (!request.TryGetProperty("id", out var id) || id.ValueKind == JsonValueKind.Null)
        {
            return new JsonRpcId((string?)null);
        }

        return id.ValueKind switch
        {
            JsonValueKind.String => new JsonRpcId(id.GetString()),
            JsonValueKind.Number when id.TryGetInt64(out var numericId) => new JsonRpcId(numericId),
            _ => throw new A2AException("Invalid JSON-RPC request: 'id' must be a string, integer, or null.", A2AErrorCode.InvalidRequest),
        };
    }

    private static T DeserializeAndValidate<T>(JsonElement jsonParamValue) where T : class
    {
        T? parms;
        try
        {
            parms = jsonParamValue.Deserialize(A2AJsonUtilities.DefaultOptions.GetTypeInfo(typeof(T))) as T;
        }
        catch (JsonException ex)
        {
            throw new A2AException($"Invalid parameters: request body could not be deserialized as {typeof(T).Name}.", ex, A2AErrorCode.InvalidParams);
        }

        if (parms is null)
        {
            throw new A2AException($"Failed to deserialize parameters as {typeof(T).Name}", A2AErrorCode.InvalidParams);
        }

        if (parms is SendMessageRequest sendMsgRequest && sendMsgRequest.Message.Parts.Count == 0)
        {
            throw new A2AException("Message parts cannot be empty", A2AErrorCode.InvalidParams);
        }

        return parms;
    }

    internal static IResult StreamResponse(IA2ARequestHandler requestHandler, JsonRpcId requestId, string method, JsonElement? parameters, CancellationToken cancellationToken)
    {
        //using var activity = A2AAspNetCoreDiagnostics.Source.StartActivity("StreamResponse", ActivityKind.Server);
        //activity?.SetTag("request.id", requestId.ToString());

        if (parameters == null)
        {
            //activity?.SetStatus(ActivityStatusCode.Error, "Invalid parameters");
            return new JsonRpcResponseResult(JsonRpcResponse.InvalidParamsResponse(requestId));
        }

        switch (method)
        {
            case A2AMethods.SubscribeToTask:
                var subscribeRequest = DeserializeAndValidate<SubscribeToTaskRequest>(parameters.Value);
                var taskEvents = requestHandler.SubscribeToTaskAsync(subscribeRequest, cancellationToken);
                return new JsonRpcStreamedResult(taskEvents, requestId);
            case A2AMethods.SendStreamingMessage:
                var sendRequest = DeserializeAndValidate<SendMessageRequest>(parameters.Value);
                var sendEvents = requestHandler.SendStreamingMessageAsync(sendRequest, cancellationToken);
                return new JsonRpcStreamedResult(sendEvents, requestId);
            default:
                //activity?.SetStatus(ActivityStatusCode.Error, "Invalid method");
                return new JsonRpcResponseResult(JsonRpcResponse.MethodNotFoundResponse(requestId));
        }
    }
}
