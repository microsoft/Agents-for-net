// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Microsoft.Agents.Hosting.AspNetCore.A2A;

/// <summary>
/// A2A to/from Activity
/// </summary>
internal static class A2AActivity
{
    public const string DefaultUserId = "unknown";

    private const string EntityTypeTemplate = "application/vnd.microsoft.entity.{0}";
    private static readonly ConcurrentDictionary<Type, Dictionary<string, JsonElement>> _schemas = [];

    public static IActivity ActivityFromMessage(string requestId, AgentTask task, AgentMessage message)
    {
        AssertionHelpers.ThrowIfNull(message, nameof(message));

        var taskId = task?.Id ?? message.TaskId ?? Guid.NewGuid().ToString("N");
        var activity = CreateActivity(taskId, message.Parts, true, true);
        activity.RequestId = requestId ?? Guid.NewGuid().ToString("N");
        activity.ChannelData = message;

        message.ContextId = message.ContextId ?? Guid.NewGuid().ToString("N");
        message.TaskId = taskId;

        return activity;
    }

    public static AgentMessage MessageFromActivity(string contextId, string taskId, IActivity activity, bool includeEntities = true)
    {
        var artifact = CreateArtifact(activity, includeEntities: includeEntities) ?? throw new ArgumentException("Invalid activity to convert to payload");

        return new AgentMessage()
        {
            TaskId = taskId,
            ContextId = contextId,
            MessageId = Guid.NewGuid().ToString("N"),
            Parts = artifact.Parts,
            Role = MessageRole.Agent
        };
    }

    public static Artifact? CreateArtifact(IActivity activity, string artifactId = null, bool includeEntities = true)
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
            artifact.Parts.Add(new TextPart()
            {
                Text = activity.Text
            });
        }

        if (activity?.Value != null)
        {
            artifact.Parts.Add(new DataPart()
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

            artifact.Parts.Add(new FilePart()
            {
                File = !string.IsNullOrEmpty(attachment.ContentUrl)
                    ? new FileContent(new Uri(attachment.ContentUrl))
                        {
                            MimeType = attachment.ContentType,
                            Name = attachment.Name,
                        }
                    : new FileContent(attachment.Content as string)
                        {
                            MimeType = attachment.ContentType,
                            Name = attachment.Name,
                        }
            });
        }

        if (includeEntities)
        {
            foreach (var entity in activity?.Entities ?? Enumerable.Empty<Entity>())
            {
                if (entity is not StreamInfo)
                {
                    if (!_schemas.TryGetValue(entity.GetType(), out var cachedMetadata))
                    {
                        cachedMetadata = entity.ToA2AMetadata(string.Format(EntityTypeTemplate, entity.Type));
                        _schemas.TryAdd(entity.GetType(), cachedMetadata);
                    }

                    artifact.Parts.Add(new DataPart
                    {
                        Metadata = cachedMetadata,
                        Data = entity.ToJsonElements()
                    });
                }
            }
        }

        return artifact;
    }

    public static Artifact? CreateArtifactFromObject(object data, string name = null, string description = null, string mediaType = null, string artifactId = null)
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
                Data = ProtocolJsonSerializer.ToObject<Dictionary<string, JsonElement>>(data),
                Metadata = data.ToA2AMetadata(mediaType ?? data.GetType().Name)
            }]
        };
    }

    public static bool HasA2AMessageContent(this IActivity activity)
    {
        return !string.IsNullOrEmpty(activity.Text)
            || (bool)activity.Attachments?.Any();
    }

    public static TaskState GetA2ATaskState(this IActivity activity)
    {
        TaskState taskState = activity.InputHint switch
        {
            InputHints.ExpectingInput => TaskState.InputRequired,
            InputHints.AcceptingInput => TaskState.Working,
            _ => TaskState.Working,
        };

        return taskState;
    }

    private static Activity CreateActivity(
        string conversationId,
        List<Part> parts,
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
            Id = DefaultUserId,
            Role = RoleTypes.User,
        };

        var activity = new Activity()
        {
            Type = ActivityTypes.Message,
            Id = Guid.NewGuid().ToString("N"),
            ChannelId = Channels.A2A,
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
                    ContentType = filePart.File.MimeType,
                    Name = filePart.File.Name,
                    ContentUrl = filePart.File.Uri.ToString(),
                    Content = filePart.File.Bytes,
                });
            }
            else if (part is DataPart dataPart)
            {
                activity.Attachments.Add(new Attachment()
                {
                    ContentType = "application/json",
                    Name = "A2A DataPart",
                    Content = ProtocolJsonSerializer.ToJson(dataPart),
                });
            }
        }

        return activity;
    }
}