// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.Extensions.Slack.Api;

public class Source
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "url";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";
}

