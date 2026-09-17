// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed record GitHubDeviceAuthorizationResponse(
    [property: JsonPropertyName("device_code")] string? DeviceCode,
    [property: JsonPropertyName("user_code")] string? UserCode,
    [property: JsonPropertyName("verification_uri")] string? VerificationUri,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("interval")] int? Interval);

internal sealed record GitHubDeviceTokenResponse(
    [property: JsonPropertyName("access_token")] string? AccessToken,
    [property: JsonPropertyName("token_type")] string? TokenType,
    [property: JsonPropertyName("scope")] string? Scope,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("error_description")] string? ErrorDescription);
