// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Extensions.MSTeams;

/// <summary>
/// Represents metadata for a Teams quoted reply.
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
    [JsonPropertyOrder(3)]
    public required QuotedReplyData QuotedReply { get; set; }
}

/// <summary>
/// Contains metadata about the message referenced by a quoted reply.
/// </summary>
public class QuotedReplyData
{
    public required string MessageId { get; set; }

    public string? SenderId { get; set; }

    public string? SenderName { get; set; }

    public string? Preview { get; set; }

    public string? Time { get; set; }

    public bool? IsReplyDeleted { get; set; }

    public bool? ValidatedMessageReference { get; set; }
}
