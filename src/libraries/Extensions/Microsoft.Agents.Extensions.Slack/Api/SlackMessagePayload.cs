// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Extensions.Slack.Api;

internal sealed record SlackMessagePayload
{
    [JsonPropertyName("channel")]
    public required string Channel { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("thread_ts")]
    public string? ThreadTs { get; init; }

    [JsonPropertyName("attachments")]
    public IReadOnlyList<SlackPostAttachment>? Attachments { get; init; }
}

internal sealed record SlackPostAttachment
{
    [JsonPropertyName("pretext")]
    public string? Pretext { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("title_link")]
    public string? TitleLink { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; init; }

    [JsonPropertyName("thumb_url")]
    public string? ThumbUrl { get; init; }

    [JsonPropertyName("fallback")]
    public string? Fallback { get; init; }

    [JsonPropertyName("callback_id")]
    public string? CallbackId { get; init; }

    [JsonPropertyName("attachment_type")]
    public string? AttachmentType { get; init; }

    [JsonPropertyName("actions")]
    public IReadOnlyList<SlackPostAction>? Actions { get; init; }

    [JsonPropertyName("fields")]
    public IReadOnlyList<SlackPostField>? Fields { get; init; }

    [JsonPropertyName("mrkdwn_in")]
    public IReadOnlyList<string>? MarkdownIn { get; init; }
}

internal sealed record SlackPostAction(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("value")] string? Value,
    [property: JsonPropertyName("style")] string? Style = null,
    [property: JsonPropertyName("options")] IReadOnlyList<SlackPostOption>? Options = null,
    [property: JsonPropertyName("selected_options")] IReadOnlyList<SlackPostOption>? SelectedOptions = null);

internal sealed record SlackPostOption(
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("value")] string? Value);

internal sealed record SlackPostField(
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("value")] string? Value);
