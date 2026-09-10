// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Extensions.MSTeams.Models;

/// <summary>
/// Represents a quoted reply in a Teams activity.
/// </summary>
[EntityName(EntityName)]
public class QuotedReplyEntity : Entity
{
    public const string EntityName = "quotedReply";

    public QuotedReplyEntity() : base(EntityName)
    {
    }

    /// <summary>
    /// Gets or sets the quoted message metadata.
    /// </summary>
    [JsonPropertyName("quotedReply")]
    public QuotedReplyData? QuotedReply { get; set; }
}

/// <summary>
/// Contains the metadata for a quoted Teams message.
/// </summary>
public class QuotedReplyData
{
    /// <summary>
    /// Gets or sets the ID of the quoted message.
    /// </summary>
    [JsonPropertyName("messageId")]
    public required string MessageId { get; set; }

    [JsonPropertyName("senderId")]
    public string? SenderId { get; set; }

    [JsonPropertyName("senderName")]
    public string? SenderName { get; set; }

    [JsonPropertyName("preview")]
    public string? Preview { get; set; }

    [JsonPropertyName("time")]
    public string? Time { get; set; }

    [JsonPropertyName("isReplyDeleted")]
    public bool? IsReplyDeleted { get; set; }

    [JsonPropertyName("validatedMessageReference")]
    public bool? ValidatedMessageReference { get; set; }
}
