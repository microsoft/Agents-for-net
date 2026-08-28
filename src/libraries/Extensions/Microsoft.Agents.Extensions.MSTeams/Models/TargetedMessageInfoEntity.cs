// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Extensions.MSTeams.Models;

/// <summary>
/// Identifies the inbound targeted message associated with a Prompt Preview response.
/// </summary>
[EntityName(EntityName)]
public class TargetedMessageInfoEntity : Entity
{
    public const string EntityName = "targetedMessageInfo";

    public TargetedMessageInfoEntity() : base(EntityName)
    {
    }

    /// <summary>
    /// Gets or sets the ID of the inbound targeted message.
    /// </summary>
    [JsonPropertyName("messageId")]
    public required string MessageId { get; set; }
}
