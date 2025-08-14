// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Hosting.A2A.JsonRpc;
using Microsoft.Agents.Hosting.A2A.Protocol;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A;

internal class A2AConverter
{
    public const string DefaultUserId = "unknown";

    private const string EntityTypeTemplate = "application/vnd.microsoft.entity.{0}";
    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, object>> _schemas = [];

    public static readonly JsonSerializerOptions SerializerOptions = new()
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

    public static string ToJson(object obj)
    {
        return JsonSerializer.Serialize(obj, SerializerOptions);
    }

    public static T ReadParams<T>(JsonRpcRequest jsonRpcPayload)
    {
        if (jsonRpcPayload.Params == null)
        {
            throw new ArgumentException("Params is null");
        }
        return JsonSerializer.SerializeToElement(jsonRpcPayload.Params, SerializerOptions).Deserialize<T>(SerializerOptions);
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

    public static IReadOnlyDictionary<string, object> ToMetadata(Type dataType, string contentType)
    {
        JsonSchemaExporterOptions exporterOptions = new()
        {
            TreatNullObliviousAsNonNullable = true,
        };

        JsonNode schema = SerializerOptions.GetJsonSchemaAsNode(dataType, exporterOptions);

        return new Dictionary<string, object>
        {
            { "mimeType", contentType},
            { "type", "object" },
            {
                "schema", schema
            }
        };
    }

    public static (IActivity, string? contextId, string? taskId, Message? message) ActivityFromRequest(JsonRpcRequest jsonRpcRequest, MessageSendParams sendParams = null, bool isStreaming = true)
    {
        if (jsonRpcRequest.Params == null)
        {
            throw new A2AException("Params is null", A2AErrors.InvalidParams);
        }

        sendParams ??= MessageSendParamsFromRequest(jsonRpcRequest);
        if (sendParams?.Message?.Parts == null)
        {
            throw new A2AException("Invalid MessageSendParams", A2AErrors.InvalidParams);
        }

        var contextId = sendParams.Message.ContextId ?? Guid.NewGuid().ToString("N");

        // taskId is our conversationId
        var taskId = sendParams.Message.TaskId ?? Guid.NewGuid().ToString("N");
        
        var activity = CreateActivity(taskId, Channels.A2A, DefaultUserId, sendParams.Message.Parts, true, isStreaming);
        activity.RequestId = jsonRpcRequest.Id.ToString();

        sendParams.Message.ContextId = contextId;
        sendParams.Message.TaskId = taskId;

        return (activity, contextId, taskId, sendParams.Message);
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

    public static TaskStatusUpdateEvent CreateStatusUpdate(string contextId, string taskId, TaskState taskState, string artifactId = null, bool isFinal = false, IActivity activity = null)
    {
        var artifact = ArtifactFromActivity(activity, artifactId);

        return new TaskStatusUpdateEvent()
        {
            TaskId = taskId,
            ContextId = contextId,
            Status = new TaskStatus()
            {
                State = taskState,
                Timestamp = DateTimeOffset.UtcNow,
                Message = artifact == null ? null : new Message() { MessageId = Guid.NewGuid().ToString("N"), Parts = artifact.Parts, Role = Message.RoleType.Agent },
            },
            Final = isFinal
        };
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="contextId"></param>
    /// <param name="taskId"></param>
    /// <param name="activity"></param>
    /// <param name="artifactId"></param>
    /// <param name="append">true means append parts to artifact; false (default) means replace.</param>
    /// <param name="lastChunk"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static TaskArtifactUpdateEvent ArtifactUpdateFromActivity(string contextId, string taskId, IActivity activity, string artifactId = null, bool append = false, bool lastChunk = false)
    {
        var artifact = ArtifactFromActivity(activity, artifactId) ?? throw new ArgumentException("Invalid activity to convert to payload");

        return new TaskArtifactUpdateEvent()
        {
            TaskId = taskId,
            ContextId = contextId,
            Artifact = artifact,
            Append = append,
            LastChunk = lastChunk
        };
    }

    public static Message MessageFromActivity(string contextId, string taskId, IActivity activity)
    {
        var artifact = ArtifactFromActivity(activity) ?? throw new ArgumentException("Invalid activity to convert to payload");

        return new Message()
        {
            TaskId = taskId,
            ContextId = contextId,
            MessageId = Guid.NewGuid().ToString("N"),
            Parts = artifact.Parts,
            Role = Message.RoleType.Agent
        };
    }

    public static AgentTask TaskFromActivity(string contextId, string taskId, TaskState taskState, IActivity activity)
    {
        var artifact = ArtifactFromActivity(activity);
        return TaskForState(contextId, taskId, taskState, artifact);
    }

    public static AgentTask TaskForState(string contextId, string taskId, TaskState taskState, Artifact artifact = null)
    {
        return new AgentTask()
        {
            Id = taskId,
            ContextId = contextId,
            Status = new TaskStatus()
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

    public static SendStreamingMessageResponse StreamingMessageResponse(RequestId requestId, object payload)
    {
        return new SendStreamingMessageResponse()
        {
            Id = requestId,
            Result = payload
        };
    }

    private static Activity CreateActivity(
        string conversationId,
        string channelId, 
        string userId, 
        ImmutableArray<Part> parts,
        bool isIngress,
        bool isStreaming = true)
    {
        var bot = new ChannelAccount
        {
            Id = "assistant",
            Role = RoleTypes.Agent,
        };

        var user = new ChannelAccount
        {
            Id = userId,
            Role = RoleTypes.User,
        };

        var activity = new Activity()
        {
            Type = ActivityTypes.Message,
            Id = Guid.NewGuid().ToString("N"),
            ChannelId = channelId,
            DeliveryMode = isStreaming ? DeliveryModes.Stream : DeliveryModes.ExpectReplies,
            Conversation = new ConversationAccount
            {
                Id = conversationId,
            },
            Recipient = isIngress ? bot : user,
            From = isIngress ? user : bot
        };

        foreach (var part in parts)
        {
            if (part is TextPart tp)
            {
                if (activity.Text == null)
                {
                    activity.Text = tp.Text;
                }
                else
                {
                    activity.Text += tp.Text;
                }
            }
            else if (part is FilePart filePart)
            {
                activity.Attachments.Add(new Attachment()
                {
                    ContentType = filePart.MimeType,
                    Name = filePart.Name,
                    ContentUrl = filePart.Uri,
                    Content = filePart.Bytes,
                });
            }
            else if (part is DataPart dataPart)
            {
                activity.Value = dataPart.Data;
            }
        }

        return activity;
    }

    public static Artifact? ArtifactFromActivity(IActivity activity, string artifactId = null, bool includeEntities = true)
    {
        if (activity == null)
        {
            return null;
        }

        var artifact = new Artifact()
        {
            ArtifactId = artifactId ?? Guid.NewGuid().ToString("N")
        };

        if (activity?.Text != null)
        {
            artifact.Parts = artifact.Parts.Add(new TextPart()
            {
                Text = activity.Text
            });
        }

        if (activity?.Value != null)
        {
            artifact.Parts = artifact.Parts.Add(new DataPart()
            {
                Data = activity.Value.ToJsonElements()
            });
        }

        foreach (var attachment in activity?.Attachments ?? Enumerable.Empty<Attachment>())
        {
            if (attachment.ContentUrl == null && attachment.Content is not string)
            {
                continue;
            }

            artifact.Parts = artifact.Parts.Add(new FilePart()
            {
                Uri = attachment.ContentUrl,
                Bytes = attachment.Content as string,
                MimeType = attachment.ContentType,
                Name = attachment.Name,

            });
        }

        if (includeEntities)
        {
            foreach (var entity in activity?.Entities ?? Enumerable.Empty<Entity>())
            {
                if (!_schemas.TryGetValue(entity.GetType(), out var cachedMetadata))
                {
                    cachedMetadata = ToMetadata(entity.GetType(), string.Format(EntityTypeTemplate, entity.Type));
                    _schemas.TryAdd(entity.GetType(), cachedMetadata);
                }

                artifact.Parts = artifact.Parts.Add(new DataPart
                {
                    Metadata = cachedMetadata,
                    Data = entity.ToJsonElements()
                });
            }
        }

        return artifact;
    }

    public static Artifact? ArtifactFromObject(object data, string name = null, string description = null, string mediaType = null, string artifactId = null)
    {
        if (data == null)
        {  
            return null; 
        }

        return new Artifact()
        {
            ArtifactId = artifactId ?? Guid.NewGuid().ToString("N"),
            Name = name,
            Description = description,
            Parts = [new DataPart()
            {
                Data = data.ToJsonElements(),
                Metadata = ToMetadata(data.GetType(), mediaType ?? data.GetType().Name)
            }]
        };
    }
}