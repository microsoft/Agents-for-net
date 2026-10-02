// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Core.Models;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Extensions.A2A.ProtocolExtensions.InTaskAuthorization;

internal sealed class InTaskAuthorizationRequest
{
    [JsonPropertyName("authorizationRequest")]
    public InTaskAuthorizationRequestDetails AuthorizationRequest { get; set; }
}

internal sealed class InTaskAuthorizationRequestDetails
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("oauth2")]
    public InTaskOAuth2Scheme OAuth2 { get; set; }

    [JsonPropertyName("requiredScopes")]
    public IList<string> RequiredScopes { get; set; }
}

internal sealed class InTaskOAuth2Scheme
{
    [JsonPropertyName("flows")]
    public OAuthFlows Flows { get; set; }
}

internal sealed class ResumeAuthRequest
{
    [JsonPropertyName("taskId")]
    public string TaskId { get; set; }

    [JsonPropertyName("contextId")]
    public string ContextId { get; set; }

    [JsonPropertyName("authorizationRequestId")]
    public string AuthorizationRequestId { get; set; }
}

internal sealed class InTaskAuthorizationContext
{
    public string TaskId { get; init; }

    public string ContextId { get; init; }

    public string AuthorizationRequestId { get; init; }

    internal IDictionary<string, TokenResponse> TokenResponses { get; } =
        new Dictionary<string, TokenResponse>(System.StringComparer.Ordinal);
}

internal sealed class ResumeAuthEventValue
{
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; init; }

    [JsonPropertyName("message")]
    public Message Message { get; init; }
}
