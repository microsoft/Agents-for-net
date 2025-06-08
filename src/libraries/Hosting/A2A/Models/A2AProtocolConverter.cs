// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Protocol;
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.A2A.Models
{
    [SerializationInit]
    public class A2AProtocolConverter
    {
        private static readonly JsonSerializerOptions s_ElementSerializerOptions = ProtocolJsonSerializer.SerializationOptions;

        public static void Init()
        {
            ProtocolJsonSerializer.ApplyExtensionOptions((inOptions) =>
            {
                return new JsonSerializerOptions(inOptions)
                {
                    AllowOutOfOrderMetadataProperties = true,
                };
            });
        }

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

        public static (IActivity, string? contextId, string? taskId) CreateActivityFromRequest(JsonRpcRequest jsonRpcPayload, string userId = "user", string channelId = "a2a", bool isStreaming = true)
        {
            if (jsonRpcPayload.Params == null)
            {
                throw new ArgumentException("Params is null");
            }

            var request = JsonSerializer.Deserialize<MessageSendParams>(JsonSerializer.SerializeToElement(jsonRpcPayload.Params, s_ElementSerializerOptions), s_ElementSerializerOptions);
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

        public static JsonRpcRequest CreateCallFromActivity(string sessionId, string taskId, IActivity activity)
        {
            var artifact = CreateArtifactFromActivity(activity);
            if (artifact == null)
            {
                throw new ArgumentException("Invalid activity to convert to payload");
            }

            var call = new MessageSendParams()
            {
                Message = new Message() { Role = "user", Parts = artifact.Parts }
            };

            var parameters = JsonSerializer.Deserialize<JsonNode>(JsonSerializer.Serialize(call, s_ElementSerializerOptions), s_ElementSerializerOptions)
                    ?? throw new ArgumentException("Failed to create record value");

            return new JsonRpcRequest()
            {
                Id = new RequestId(activity.Id ?? Guid.NewGuid().ToString("N")),
                Method = "tasks/send",
                Params = parameters,
            };
        }

        public static string CreateStreamStatusUpdateFromActivity(string requestId, string contextId, string taskId, IActivity activity)
        {
            var artifact = CreateArtifactFromActivity(activity) ?? throw new ArgumentException("Invalid activity to convert to payload");

            var task = new TaskStatusUpdateEvent()
            {
                TaskId = taskId,
                ContextId = contextId,
                Status = new TaskStatus()
                {
                    State = string.Equals(InputHints.ExpectingInput, activity.InputHint, StringComparison.OrdinalIgnoreCase) ? TaskState.InputRequired : TaskState.Working,
                    Message = new Message()
                    {
                        MessageId = Guid.NewGuid().ToString("N"),
                        Parts = artifact.Parts,
                        Role = RoleTypes.Agent
                    }
                }
            };

            return ProtocolJsonSerializer.ToJson(new SendStreamingMessageResponse()
            {
                Id = requestId,
                Result = task
            });
        }

        public static string CreateStreamArtifactUpdateFromActivity(string requestId, string contextId, string taskId, IActivity activity, bool append = false, bool lastChunk = false)
        {
            var artifact = CreateArtifactFromActivity(activity) ?? throw new ArgumentException("Invalid activity to convert to payload");

            var task = new TaskArtifactUpdateEvent()
            {
                TaskId = taskId,
                ContextId = contextId,
                Artifact = artifact,
                Append = append,
                LastChunk = lastChunk
            };

            return ProtocolJsonSerializer.ToJson(new SendStreamingMessageResponse()
            {
                Id = requestId,
                Result = task
            });
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

            return ProtocolJsonSerializer.ToJson(new SendStreamingMessageResponse()
            {
                Id = requestId,
                Result = message
            });
        }

        public static string ToJson(object obj)
        {
            return ProtocolJsonSerializer.ToJson(obj);
        }

        private static bool IsTerminalState(string? state)
            => state != TaskState.Submitted && state != TaskState.Working && state != TaskState.InputRequired;

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

        public static Artifact? CreateArtifactFromActivity(IActivity activity)
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
                        Data = activity.Value
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

            if (artifact != Artifact.Empty)
            {
                return artifact;
            }

            return null;
        }
    }
}