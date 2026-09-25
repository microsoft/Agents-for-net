// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace A2AAgent;

/// <summary>
/// Contains the Microsoft Graph profile fields returned by the sample.
/// </summary>
/// <param name="DisplayName">The user's display name.</param>
/// <param name="UserPrincipalName">The user's principal name.</param>
public sealed record GraphProfile(
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("userPrincipalName")] string UserPrincipalName);
