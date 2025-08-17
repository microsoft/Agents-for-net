// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Hosting.A2A.JsonRpc;
using Microsoft.Agents.Hosting.A2A.Protocol;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.A2A;

/// <summary>
/// Concerning A2A model helpers for extensions, creation, serialization.
/// </summary>
internal static class A2AModel
{
    public static bool IsTerminal(this AgentTask task)
    {
        return task.Status.State == TaskState.Completed
            || task.Status.State == TaskState.Canceled
            || task.Status.State == TaskState.Rejected
            || task.Status.State == TaskState.Failed;
    }

    public static AgentTask WithHistoryTrimmedTo(this AgentTask task, int? toLength)
    {
        if (!toLength.HasValue || toLength.Value < 0 || task.History.Value.Length <= 0 || task.History.Value.Length <= toLength.Value)
        {
            return task;
        }

        return new AgentTask
        {
            Id = task.Id,
            ContextId = task.ContextId,
            Status = task.Status,
            Artifacts = task.Artifacts,
            Metadata = task.Metadata,
            History = [.. task.History.Value.Skip(task.History.Value.Length - toLength.Value)],
        };
    }

    public static IReadOnlyDictionary<string, object> ToA2AMetadata(this object data, string contentType)
    {
        JsonSchemaExporterOptions exporterOptions = new()
        {
            TreatNullObliviousAsNonNullable = true,
        };

        JsonNode schema = SerializerOptions.GetJsonSchemaAsNode(data.GetType(), exporterOptions);

        return new Dictionary<string, object>
        {
            { "mimeType", contentType},
            { "type", "object" },
            {
                "schema", schema
            }
        };
    }

    public static AgentTask TaskForState(string contextId, string taskId, TaskState taskState, Artifact artifact = null)
    {
        return new AgentTask()
        {
            Id = taskId,
            ContextId = contextId,
            Status = new Protocol.TaskStatus()
            {
                State = taskState,
                Timestamp = DateTimeOffset.UtcNow,
                Message = artifact == null
                    ? null
                    : new Message()
                    {
                        TaskId = taskId,
                        ContextId = contextId,
                        MessageId = Guid.NewGuid().ToString("N"),
                        Parts = artifact.Parts,
                        Role = Message.RoleType.Agent
                    }
            },
        };
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        AllowOutOfOrderMetadataProperties = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower)
        }
    };

    #region From Request
    public static async Task<T?> ReadRequestAsync<T>(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            return await JsonSerializer.DeserializeAsync<T>(request.Body, SerializerOptions);
        }
        catch (Exception ex)
        {
            throw new A2AException(ex.Message, ex, A2AErrors.InvalidRequest);
        }
    }

    public static MessageSendParams MessageSendParamsFromRequest(JsonRpcRequest jsonRpcRequest)
    {
        MessageSendParams sendParams;

        try
        {
            sendParams = JsonSerializer.SerializeToElement(jsonRpcRequest.Params, SerializerOptions).Deserialize<MessageSendParams>(SerializerOptions);
        }
        catch (Exception ex)
        {
            throw new A2AException(ex.Message, A2AErrors.ParseError);
        }

        if (sendParams?.Message?.Parts == null)
        {
            throw new A2AException("Invalid MessageSendParams", A2AErrors.InvalidParams);
        }

        return sendParams;
    }

    public static T ReadParams<T>(JsonRpcRequest jsonRpcPayload)
    {
        if (jsonRpcPayload.Params == null)
        {
            throw new ArgumentException("Params is null");
        }
        return JsonSerializer.SerializeToElement(jsonRpcPayload.Params, SerializerOptions).Deserialize<T>(SerializerOptions);
    }
    #endregion

    #region To Response
    public static string ToJson(object obj)
    {
        return JsonSerializer.Serialize(obj, SerializerOptions);
    }

    public static JsonRpcResponse CreateResponse(RequestId requestId, object result)
    {
        return new JsonRpcResponse()
        {
            Id = requestId,
            Result = JsonSerializer.SerializeToNode(result, SerializerOptions)
        };
    }

    public static JsonRpcError CreateErrorResponse(JsonRpcRequest jsonRpcPayload, int code, string message)
    {
        var id = jsonRpcPayload != null ? jsonRpcPayload.Id : new RequestId();

        return new JsonRpcError()
        {
            Id = id,
            Error = new JsonRpcErrorDetail()
            {
                Code = code,
                Message = message
            }
        };
    }

    public static SendStreamingMessageResponse StreamingMessageResponse(RequestId requestId, object payload)
    {
        return new SendStreamingMessageResponse()
        {
            Id = requestId,
            Result = payload
        };
    }
    #endregion
}