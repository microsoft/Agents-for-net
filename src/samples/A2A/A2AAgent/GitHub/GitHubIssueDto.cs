// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace A2AAgent;

internal sealed record GitHubIssueDto(
    [property: JsonPropertyName("html_url")] string HtmlUrl,
    [property: JsonPropertyName("number")] int Number,
    [property: JsonPropertyName("repository_url")] string RepositoryUrl,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("pull_request")] JsonElement PullRequest);
