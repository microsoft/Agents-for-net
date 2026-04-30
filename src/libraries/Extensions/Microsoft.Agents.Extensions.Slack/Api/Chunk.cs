// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Extensions.Slack.Api;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(MarkdownTextChunk), "markdown_text")]
[JsonDerivedType(typeof(BlocksChunk), "blocks")]
[JsonDerivedType(typeof(TaskUpdateChunk), "task_update")]
public abstract class Chunk { }

public class MarkdownTextChunk(string text) : Chunk
{
    public static implicit operator MarkdownTextChunk(string? text) => new(text ?? "");

    public string text { get; set; } = text;
}

public class BlocksChunk : Chunk
{
    public IList<object> blocks { get; set; }
}

public class TaskUpdateChunk(string id, string title, string status = SlackTaskStatus.InProgress) : Chunk
{
    public string id { get; set; } = id;

    public string title { get; set; } = title;

    public string status { get; set; } = status;

    public string? details { get; set; }

    public string? output { get; set; }

    public IList<Source>? sources { get; set; }
}