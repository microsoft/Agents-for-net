// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Hosting.A2A.JsonRpc;
using Microsoft.Agents.Hosting.A2A.Protocol;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.A2A
{
    internal class A2AConverter
    {
        private const string EntityTypeTemplate = "application/vnd.microsoft.entity.{0}";
        private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, object>> _schemas = [];

        private static readonly JsonSerializerOptions s_SerializerOptions = new()
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

        public static async Task<T?> ReadRequestAsync<T>(HttpRequest request)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(request);

                //using var memoryStream = new MemoryStream();
                //await request.Body.CopyToAsync(memoryStream).ConfigureAwait(false);
                //memoryStream.Seek(0, SeekOrigin.Begin);

                return await JsonSerializer.DeserializeAsync<T>(request.Body, s_SerializerOptions);
            }
            catch (JsonException)
            {
                return default;
            }
        }

        public static async Task WriteResponseAsync(HttpResponse response, object payload, bool streamed = false, HttpStatusCode code = HttpStatusCode.OK, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(response);
            ArgumentNullException.ThrowIfNull(payload);

            response.StatusCode = (int)code;
            
            var json = JsonSerializer.Serialize(payload, s_SerializerOptions);
            if (!streamed)
            {
                response.ContentType = "application/json";
            }
            else
            {
                response.ContentType = "text/event-stream";
                json = $"data: {json}\r\n\r\n";
            }

            await response.Body.WriteAsync(Encoding.UTF8.GetBytes(json), cancellationToken).ConfigureAwait(false);
            await response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        public static T ReadParams<T>(JsonRpcRequest jsonRpcPayload)
        {
            if (jsonRpcPayload.Params == null)
            {
                throw new ArgumentException("Params is null");
            }
            return JsonSerializer.SerializeToElement(jsonRpcPayload.Params, s_SerializerOptions).Deserialize<T>(s_SerializerOptions);
        }

        public static JsonRpcResponse CreateResponse(JsonRpcRequest jsonRpcPayload, object result)
        {
            return new JsonRpcResponse()
            {
                Id = jsonRpcPayload.Id,
                Result = JsonSerializer.SerializeToNode(result, s_SerializerOptions)
            };
        }

        public static JsonRpcResponse CreateErrorResponse(JsonRpcRequest jsonRpcPayload, int code, string message)
        {
            var id = jsonRpcPayload != null ? jsonRpcPayload.Id : new RequestId();

            return new JsonRpcResponse()
            {
                Id = id,
                Result = JsonSerializer.SerializeToNode(new JsonRpcError
                {
                    Id = id,
                    Error = new JsonRpcErrorDetail
                    {
                        Code = code,
                        Message = message
                    }
                }, s_SerializerOptions)
            };
        }

        public static IReadOnlyDictionary<string, object> ToMetadata(Type dataType, string contentType)
        {
            JsonSchemaExporterOptions exporterOptions = new()
            {
                TreatNullObliviousAsNonNullable = true,
            };

            JsonNode schema = s_SerializerOptions.GetJsonSchemaAsNode(dataType, exporterOptions);

            return new Dictionary<string, object>
            {
                { "mimeType", contentType},
                { "type", "object" },
                {
                    "schema", schema
                }
            };
        }

        public static (IActivity, string? contextId, string? taskId, Message? message) ActivityFromRequest(JsonRpcRequest jsonRpcPayload, string userId = "user", string channelId = "a2a", bool isStreaming = true)
        {
            if (jsonRpcPayload.Params == null)
            {
                throw new ArgumentException("Params is null");
            }

            var request = JsonSerializer.SerializeToElement(jsonRpcPayload.Params, s_SerializerOptions).Deserialize<MessageSendParams>(s_SerializerOptions);
            if (request?.Message?.Parts == null)
            {
                throw new ArgumentException("Failed to parse request body");
            }

            var contextId = request.Message.ContextId ?? Guid.NewGuid().ToString("N");
            var taskId = request.Message.TaskId ?? Guid.NewGuid().ToString("N");
            var activity = CreateActivity(contextId, channelId, userId, request.Message.Parts, true, isStreaming);
            activity.RequestId = jsonRpcPayload.Id.ToString();
            activity.ChannelData = jsonRpcPayload;

            var message = request.Message with
            {
                ContextId = contextId,
                TaskId = taskId,
            };

            return (activity, contextId, taskId, message);
        }

        public static TaskStatusUpdateEvent StatusUpdateFromActivity(string contextId, string taskId, TaskState taskState, string artifactId = null, bool isFinal = false, IActivity activity = null)
        {
            var artifact = ArtifactFromActivity(activity, artifactId);

            return new TaskStatusUpdateEvent()
            {
                TaskId = taskId,
                ContextId = contextId,
                Status = new Protocol.TaskStatus()
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

        public static TaskResponse TaskFromActivity(string contextId, string taskId, TaskState taskState, IActivity activity)
        {
            var artifact = ArtifactFromActivity(activity);
            return TaskForState(contextId, taskId, taskState, artifact);
        }

        public static TaskResponse TaskForState(string contextId, string taskId, TaskState taskState, Artifact artifact = null)
        {
            return new TaskResponse()
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

        public static SendStreamingMessageResponse StreamingMessageResponse(string requestId, object payload)
        {
            return new SendStreamingMessageResponse()
            {
                Id = requestId,
                Result = payload
            };
        }

        public static string ToJson(object obj)
        {
            return JsonSerializer.Serialize(obj, s_SerializerOptions);
        }

        private static Activity CreateActivity(
            string? contextId,
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
                    Id = contextId,
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
            var artifact = Artifact.Empty;

            if (activity?.Text != null)
            {
                artifact = artifact with
                {
                    Parts = artifact.Parts.Add(new TextPart()
                    {
                        Text = activity.Text
                    })
                };
            }

            if (activity?.Value != null)
            {
                artifact = artifact with
                {
                    Parts = artifact.Parts.Add(new DataPart()
                    {
                        Data = activity.Value.ToJsonElements()
                    })
                };
            }

            foreach (var attachment in activity?.Attachments ?? Enumerable.Empty<Attachment>())
            {
                if (attachment.ContentUrl == null && attachment.Content is not string stringContent)
                {
                    continue;
                }

                artifact = artifact with
                {
                    Parts = artifact.Parts.Add(new FilePart()
                    {
                        Uri = attachment.ContentUrl,
                        Bytes = attachment.Content as string,
                        MimeType = attachment.ContentType,
                        Name = attachment.Name,

                    })
                };
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

                    artifact = artifact with
                    {
                        Parts = artifact.Parts.Add(new DataPart
                        {
                            Metadata = cachedMetadata,
                            Data = entity.ToJsonElements()
                        })
                    };
                }
            }

            if (artifact != Artifact.Empty)
            {
                artifact = artifact with
                {
                    ArtifactId = artifactId ?? Guid.NewGuid().ToString("N")
                };
                return artifact;
            }

            return null;
        }
    }
}