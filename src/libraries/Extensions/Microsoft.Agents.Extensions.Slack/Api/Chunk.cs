// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Extensions.Slack.Api;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(MarkdownTextChunk), "markdown_text")]
[JsonDerivedType(typeof(TaskUpdateChunk), "task_update")]
public abstract class Chunk { }

public class MarkdownTextChunk : Chunk
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = "";
}

public class TaskUpdateChunk : Chunk
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = TaskStatus.Pending;

    [JsonPropertyName("details")]
    public string? Details { get; set; }

    [JsonPropertyName("output")]
    public string? Output { get; set; }

    [JsonPropertyName("sources")]
    public List<Source>? Sources { get; set; }
}