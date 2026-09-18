// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
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

    public string AccessToken { get; init; }

    internal bool CredentialValidated { get; init; }

    internal string HandlerName { get; set; }

    internal IDictionary<string, TokenResponse> TokenResponses { get; } =
        new Dictionary<string, TokenResponse>(System.StringComparer.Ordinal);

    internal bool Accepted { get; set; }
}

internal sealed class InTaskAuthorizationState : IStoreItem
{
    public string HandlerName { get; set; }

    public string TaskId { get; set; }

    public string ContextId { get; set; }

    public string AuthorizationRequestId { get; set; }

    public string OriginalRequestId { get; set; }

    public bool IsResuming { get; set; }

    public string ETag { get; set; }
}
