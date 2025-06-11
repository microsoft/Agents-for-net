// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Protocol;
using Newtonsoft.Json.Schema.Generation;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.A2A.Models
{
    public class A2AProtocolConverter
    {
        private static readonly JsonSerializerOptions s_SerializerOptions = new()
        {
            AllowOutOfOrderMetadataProperties = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public static async Task<T?> ReadRequestAsync<T>(HttpRequest request)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(request);

                using var memoryStream = new MemoryStream();
                await request.Body.CopyToAsync(memoryStream).ConfigureAwait(false);
                memoryStream.Seek(0, SeekOrigin.Begin);

                return ProtocolJsonSerializer.ToObject<T>(memoryStream);
            }
            catch (JsonException)
            {
                return default;
            }
        }

        public static IDictionary<string, object> ToMetadata(object data, string contentType)
        {
            JSchemaGenerator generator = new();
            generator.ContractResolver = new CamelCasePropertyNamesContractResolver();
            generator.DefaultRequired = Newtonsoft.Json.Required.Default;
            generator.SchemaIdGenerationHandling = SchemaIdGenerationHandling.None;

            var schema = JsonSerializer.Deserialize<Dictionary<string, JsonNode>>(generator.Generate(data.GetType()).ToString());
            schema.Remove("definitions");
            if (schema.TryGetValue("properties", out var properties))
            {
                // skip Core Model additional properties
                var jsonObject = properties.AsObject();
                jsonObject.Remove("properties");
                jsonObject.Remove("$type");
                jsonObject.Remove("$typeAssembly");
            }

            return new Dictionary<string, object>
            {
                { "mimeType", "application/json"},
                { "contentType", contentType },
                { "type", "object" },
                {
                    "schema",
                    new Dictionary<string, object> 
                    {
                        { "type", "object" },
                        { "properties", schema["properties"] } 
                    }
                }
            };
        }

        public static (IActivity, string? contextId, string? taskId) CreateActivityFromRequest(JsonRpcRequest jsonRpcPayload, string userId = "user", string channelId = "a2a", bool isStreaming = true)
        {
            if (jsonRpcPayload.Params == null)
            {
                throw new ArgumentException("Params is null");
            }

            var request = JsonSerializer.Deserialize<MessageSendParams>(JsonSerializer.SerializeToElement(jsonRpcPayload.Params, s_SerializerOptions), s_SerializerOptions);
            if (request?.Message?.Parts == null)
            {
                throw new ArgumentException("Failed to parse request body");
            }

            var isIngress = true;
            var contextId = request.Message.ContextId ?? Guid.NewGuid().ToString("N");
            var taskId = request.Message.TaskId ?? Guid.NewGuid().ToString("N");
            var activity = CreateActivity(contextId, channelId, userId, request.Message.Parts, isIngress, isStreaming);
            activity.ChannelData = jsonRpcPayload;
            return (activity, contextId, taskId);
        }

        public static string CreateStreamStatusUpdateFromActivity(string requestId, string contextId, string taskId, string taskState, string artifactId = null, bool isFinal = false, IActivity activity = null)
        {
            var artifact = CreateArtifactFromActivity(activity, artifactId);

            var task = new TaskStatusUpdateEvent()
            {
                TaskId = taskId,
                ContextId = contextId,
                Status = new TaskStatus()
                {
                    State = taskState,
                    Timestamp = DateTimeOffset.UtcNow,
                    Message = artifact == null ? null : new Message() { MessageId = Guid.NewGuid().ToString("N"), Parts = artifact.Parts, Role = RoleTypes.Agent },
                },
                Final = isFinal
            };

            return ToJson(
                new SendStreamingMessageResponse()
                {
                    Id = requestId,
                    Result = task
                });
        }

        public static string CreateStreamArtifactUpdateFromActivity(string requestId, string contextId, string taskId, IActivity activity, string artifactId = null, bool append = false, bool lastChunk = false)
        {
            var artifact = CreateArtifactFromActivity(activity, artifactId) ?? throw new ArgumentException("Invalid activity to convert to payload");

            var task = new TaskArtifactUpdateEvent()
            {
                TaskId = taskId,
                ContextId = contextId,
                Artifact = artifact,
                Append = append,
                LastChunk = lastChunk
            };

            return ToJson(new SendStreamingMessageResponse() { Id = requestId, Result = task });
        }

        public static string CreateStreamMessageFromActivity(string requestId, string contextId, string taskId, IActivity activity)
        {
            var artifact = CreateArtifactFromActivity(activity) ?? throw new ArgumentException("Invalid activity to convert to payload");

            var message = new Message()
            {
                TaskId = taskId,
                ContextId = contextId,
                MessageId = Guid.NewGuid().ToString("N"),
                Parts = artifact.Parts,
                Role = RoleTypes.Agent
            };

            return ToJson(new SendStreamingMessageResponse()
            {
                Id = requestId,
                Result = message
            });
        }

        public static string CreateStreamTaskFromActivity(string requestId, string contextId, string taskId, string taskState, IActivity activity = null)
        {
            var artifact = CreateArtifactFromActivity(activity);

            var message = new TaskResponse()
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
                                Role = RoleTypes.Agent
                            }
                },
            };

            return ToJson(new SendStreamingMessageResponse()
            {
                Id = requestId,
                Result = message
            });
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

        public static Artifact? CreateArtifactFromActivity(IActivity activity, string artifactId = null)
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
                        Data = ProtocolJsonSerializer.ToJsonElements(activity.Value)
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

            /*
            foreach (var entity in activity?.Entities ?? Enumerable.Empty<Entity>())
            {
                artifact = artifact with
                {
                    Parts = artifact.Parts.Add(new DataPart
                    {
                        Metadata = ToMetadata(entity, $"application/vnd.microsoft.entity.{entity.Type}"),
                        Data = ProtocolJsonSerializer.ToJsonElements(entity)
                    })
                };
            }
            */

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