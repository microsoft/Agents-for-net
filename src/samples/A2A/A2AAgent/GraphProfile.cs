// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace A2AAgent;

public sealed record GraphProfile(
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("userPrincipalName")] string UserPrincipalName);
