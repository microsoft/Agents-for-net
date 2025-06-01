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
using System.Text.Json.Serialization;
using System.Threading.Tasks;

#nullable enable

namespace A2AAgent
{
    internal class A2AProtocolConverter
    {
        private static readonly JsonSerializerOptions s_ElementSerializerOptions = ProtocolJsonSerializer.SerializationOptions; // new JsonSerializerOptions(ElementSerializer.CreateOptions())
        //{
        //    // A2A supports out of order kind tags. enabling this ensures that we can use polymorphic (de)serialization
        //    AllowOutOfOrderMetadataProperties = true,
        //};


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

        public static (IActivity, string? contextId, string? taskId) CreateActivityFromRequest(JsonRpcRequest jsonRpcPayload, string userId = "user", string channelId = "a2a")
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
            return (CreateActivity(contextId, channelId, userId, request.Message.Parts, isIngress), contextId, taskId);
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

        private bool IsTerminalState(string? state)
            => state != TaskState.Submitted && state != TaskState.Working && state != TaskState.InputRequired;

        private static Activity CreateActivity(
            string? contextId,
            string channelId, 
            string userId, 
            ImmutableArray<Part> parts,
            bool isIngress)
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
                DeliveryMode = DeliveryModes.Stream,
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

    public static class TaskState
    {
        public const string Submitted = "submitted";
        public const string Working = "working";
        public const string InputRequired = "input-required";
        public const string Completed = "completed";
        public const string Cancelled = "canceled";
        public const string Failed = "failed";
        public const string Rejected = "rejected";
        public const string AuthRequired = "auth-required";
        public const string Unknown = "unknown";
    }

    public record TaskResponse
    {
        public string Kind => "task";

        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("contextId")]
        public string? ContextId { get; init; }

        [JsonPropertyName("status")]
        public TaskStatus? Status { get; init; }

        [JsonPropertyName("artifacts")]
        public ImmutableArray<Artifact>? Artifacts { get; init; }

        [JsonPropertyName("history")]
        public ImmutableArray<Message>? History { get; init; }

        //metadata
    }

    public record MessageSendParams
    {
        [JsonPropertyName("message")]
        public required Message Message { get; init; }

        [JsonPropertyName("configuration")]
        public MessageSendConfiguration? Configuration { get; init; }

        //metadata
    }

    public record MessageSendConfiguration
    {
        [JsonPropertyName("acceptedOutputModes")]
        public ImmutableArray<string>? AcceptedOutputModes { get; init; }

        [JsonPropertyName("blocking")]
        public bool? Blocking { get; init; }

        [JsonPropertyName("historyLength")]
        public int? HistoryLength { get; init; }

        //PushNotificationConfig? pushNotificationConfig
    }

    public record SendStreamingMessageResponse
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc => "2.0";

        /// <summary>
        /// Matches the id from the originating tasks/sendSubscribe or tasks/resubscribe TaskSendParams.
        /// </summary>
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        /// <summary>
        /// TaskArtifactUpdateEvent or TaskStatusUpdateEvent
        /// </summary>
        [JsonPropertyName("result")]
        public required object Result { get; init; }
    }

    public record TaskArtifactUpdateEvent
    {
        public string Kind => "artifact-update";

        /// <summary>
        /// Task ID being updated
        /// </summary>
        [JsonPropertyName("taskId")]
        public required string TaskId { get; init; }

        /// <summary>
        /// Context ID the task is associated with
        /// </summary>
        [JsonPropertyName("contextId")]
        public required string ContextId { get; init; }

        [JsonPropertyName("artifact")]
        public required Artifact Artifact { get; init; }

        [JsonPropertyName("append")]
        public bool? Append { get; init; }

        [JsonPropertyName("lastChunk")]
        public bool? LastChunk { get; init; }

        //metadata
    }

    public record TaskStatusUpdateEvent
    {
        public string Kind => "status-update";

        /// <summary>
        /// Task ID being updated
        /// </summary>
        [JsonPropertyName("taskId")]
        public required string TaskId { get; init; }

        /// <summary>
        /// Context ID the task is associated with
        /// </summary>
        [JsonPropertyName("contextId")]
        public required string ContextId { get; init; }

        [JsonPropertyName("status")]
        public required TaskStatus Status { get; init; }

        [JsonPropertyName("final")]
        public bool? Final { get; init; }

        //metadata
    }

    public record TaskStatus
    {
        [JsonPropertyName("state")]
        public required string State { get; init; }

        [JsonPropertyName("message")]
        public Message? Message { get; init; }

        [JsonPropertyName("timestamp")]
        public DateTimeOffset? Timestamp { get; init; }
    }


    public record Message
    {
        public string Kind => "message";

        [JsonPropertyName("role")]
        public required string Role { get; init; }

        [JsonPropertyName("parts")]
        public required ImmutableArray<Part> Parts { get; init; }

        [JsonPropertyName("messageId")]
        public string? MessageId { get; init; }

        [JsonPropertyName("taskId")]
        public string? TaskId { get; init; }

        [JsonPropertyName("contextId")]
        public string? ContextId { get; init; }

        //metadata
        //referenceTaskIds
    }

    public record Artifact
    {
        public static Artifact Empty = new() { ArtifactId = Guid.NewGuid().ToString("N") };

        [JsonPropertyName("artifactId")]
        public required string ArtifactId { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("parts")]
        public ImmutableArray<Part> Parts { get; init; } = ImmutableArray<Part>.Empty;
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
    [JsonDerivedType(typeof(TextPart), typeDiscriminator: "text")]
    [JsonDerivedType(typeof(FilePart), typeDiscriminator: "file")]
    [JsonDerivedType(typeof(DataPart), typeDiscriminator: "data")]
    public abstract record Part
    {
    }

    public record TextPart : Part
    {
        [JsonPropertyName("text")]
        public required string Text { get; init; }

        //metadata
    }


    public record FilePart : Part
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("mimeType")]
        public string? MimeType { get; init; }

        [JsonPropertyName("bytes")]
        public string? Bytes { get; init; }

        [JsonPropertyName("uri")]
        public string? Uri { get; init; }
    }

    public record DataPart : Part
    {
        [JsonPropertyName("data")]
        public required object Data { get; init; }
    }

}