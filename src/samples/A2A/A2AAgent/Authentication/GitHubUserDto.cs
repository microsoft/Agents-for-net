// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace A2AAgent;

internal sealed record GitHubUserDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("login")] string Login,
    [property: JsonPropertyName("name")] string? Name);
