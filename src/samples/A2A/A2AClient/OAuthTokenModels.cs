// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed record OAuthDeviceAuthorizationResponse(
    [property: JsonPropertyName("device_code")] string? DeviceCode,
    [property: JsonPropertyName("user_code")] string? UserCode,
    [property: JsonPropertyName("verification_uri")] string? VerificationUri,
    [property: JsonPropertyName("verification_uri_complete")] string? VerificationUriComplete,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("interval")] int? Interval,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("error_description")] string? ErrorDescription);

internal sealed record OAuthTokenResponse(
    [property: JsonPropertyName("access_token")] string? AccessToken,
    [property: JsonPropertyName("token_type")] string? TokenType,
    [property: JsonPropertyName("scope")] string? Scope,
    [property: JsonPropertyName("expires_in")] int? ExpiresIn,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("error_description")] string? ErrorDescription);

internal sealed record OAuthAccessToken(
    string AccessToken,
    TimeSpan? ExpiresIn,
    string? RefreshToken = null);
