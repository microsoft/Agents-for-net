// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using Microsoft.Agents.Extensions.Slack.Api;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace Microsoft.Agents.Extensions.Slack.Tests;

public class ChunkTests
{
    private static readonly JsonSerializerOptions IgnoreNullOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void MarkdownTextChunk_Serializes_WithTypeDiscriminator()
    {
        Chunk chunk = new MarkdownTextChunk("hello");

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(chunk));

        Assert.Equal("markdown_text", json.RootElement.GetProperty("type").GetString());
        Assert.Equal("hello", json.RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public void MarkdownTextChunk_ImplicitConversion_FromString()
    {
        MarkdownTextChunk chunk = "hello";

        Assert.Equal("hello", chunk.text);
    }

    [Fact]
    public void MarkdownTextChunk_ImplicitConversion_FromNull()
    {
        string? value = null;

        MarkdownTextChunk chunk = value;

        Assert.Equal(string.Empty, chunk.text);
    }

    [Fact]
    public void BlocksChunk_Serializes_WithTypeDiscriminator()
    {
        Chunk chunk = new BlocksChunk
        {
            blocks = new List<object>
            {
                new { type = "section", text = new { type = "mrkdwn", text = "hello" } }
            }
        };

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(chunk));

        Assert.Equal("blocks", json.RootElement.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Array, json.RootElement.GetProperty("blocks").ValueKind);
    }

    [Fact]
    public void TaskUpdateChunk_Serializes_WithAllFields()
    {
        Chunk chunk = new TaskUpdateChunk("task-1", "Investigate", SlackTaskStatus.Complete)
        {
            details = "Done",
            output = "Result",
            sources =
            [
                new Source
                {
                    text = "Docs",
                    url = "https://example.com"
                }
            ]
        };

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(chunk));
        var source = json.RootElement.GetProperty("sources")[0];

        Assert.Equal("task_update", json.RootElement.GetProperty("type").GetString());
        Assert.Equal("task-1", json.RootElement.GetProperty("id").GetString());
        Assert.Equal("Investigate", json.RootElement.GetProperty("title").GetString());
        Assert.Equal(SlackTaskStatus.Complete, json.RootElement.GetProperty("status").GetString());
        Assert.Equal("Done", json.RootElement.GetProperty("details").GetString());
        Assert.Equal("Result", json.RootElement.GetProperty("output").GetString());
        Assert.Equal("url", source.GetProperty("type").GetString());
        Assert.Equal("Docs", source.GetProperty("text").GetString());
        Assert.Equal("https://example.com", source.GetProperty("url").GetString());
    }

    [Fact]
    public void TaskUpdateChunk_DefaultStatus_IsInProgress()
    {
        var chunk = new TaskUpdateChunk("task-1", "Investigate");

        Assert.Equal(SlackTaskStatus.InProgress, chunk.status);
    }

    [Fact]
    public void TaskUpdateChunk_NullableFields_OmittedWhenNull()
    {
        Chunk chunk = new TaskUpdateChunk("task-1", "Investigate");

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(chunk, IgnoreNullOptions));

        Assert.False(json.RootElement.TryGetProperty("details", out _));
        Assert.False(json.RootElement.TryGetProperty("output", out _));
        Assert.False(json.RootElement.TryGetProperty("sources", out _));
    }

    [Fact]
    public void Chunk_Deserialize_Polymorphic_MarkdownText()
    {
        var chunk = JsonSerializer.Deserialize<Chunk>("""{"type":"markdown_text","text":"hi"}""");

        var markdownChunk = Assert.IsType<MarkdownTextChunk>(chunk);
        Assert.Equal("hi", markdownChunk.text);
    }

    [Fact]
    public void Chunk_Deserialize_Polymorphic_TaskUpdate()
    {
        var chunk = JsonSerializer.Deserialize<Chunk>(
            """{"type":"task_update","id":"task-1","title":"Investigate","status":"complete","details":"Done","output":"Result"}""");

        var taskChunk = Assert.IsType<TaskUpdateChunk>(chunk);
        Assert.Equal("task-1", taskChunk.id);
        Assert.Equal("Investigate", taskChunk.title);
        Assert.Equal(SlackTaskStatus.Complete, taskChunk.status);
        Assert.Equal("Done", taskChunk.details);
        Assert.Equal("Result", taskChunk.output);
    }
}
