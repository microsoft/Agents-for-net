// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2AClient.A2A;

internal sealed class A2AInTaskAuthorizationClient
{
    internal const string ExtensionUri = "https://schemas.microsoft.com/agents/a2a/extensions/in-task-authorization/v1";
    internal const string ExtensionHeader = "A2A-Extensions";
    internal const string TokenHeader = "x-a2a-intask-authorization";
    private readonly HttpClient _httpClient;
    private readonly IA2AAccessTokenProvider _accessTokenProvider;
    private readonly Uri _interfaceUri;
    private readonly bool _isJsonRpc;

    private A2AInTaskAuthorizationClient(
        HttpClient httpClient,
        IA2AAccessTokenProvider accessTokenProvider,
        Uri interfaceUri,
        bool isJsonRpc)
    {
        _httpClient = httpClient;
        _accessTokenProvider = accessTokenProvider;
        _interfaceUri = interfaceUri;
        _isJsonRpc = isJsonRpc;
    }

    internal static A2AInTaskAuthorizationClient Create(
        AgentCard card,
        HttpClient httpClient,
        IA2AAccessTokenProvider accessTokenProvider,
        Uri agentUrl)
    {
        Uri agentOrigin = A2AAgentOrigin.FromAgentUrl(agentUrl);
        AgentInterface interfaceDefinition = (card.SupportedInterfaces ?? [])
            .First(item =>
                (string.Equals(item.ProtocolBinding, ProtocolBindingNames.JsonRpc, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.ProtocolBinding, ProtocolBindingNames.HttpJson, StringComparison.OrdinalIgnoreCase))
                && Uri.TryCreate(item.Url, UriKind.Absolute, out Uri? uri)
                && A2AAgentOrigin.IsSameOrigin(agentOrigin, uri));
        var interfaceUri = new Uri(interfaceDefinition.Url, UriKind.Absolute);

        return new A2AInTaskAuthorizationClient(
            httpClient,
            accessTokenProvider,
            interfaceUri,
            string.Equals(interfaceDefinition.ProtocolBinding, ProtocolBindingNames.JsonRpc, StringComparison.OrdinalIgnoreCase));
    }

    internal async Task<AgentTask?> ResumeIfRequiredAsync(AgentTask? task, CancellationToken cancellationToken)
    {
        var handledAuthorizationRequests = new HashSet<string>(StringComparer.Ordinal);

        while (task?.Status.State == TaskState.AuthRequired
            && task.Status.Message?.Metadata is not null
            && task.Status.Message.Metadata.TryGetValue(ExtensionUri, out JsonElement metadata))
        {
            InTaskAuthorizationMetadata authorization = metadata.Deserialize<InTaskAuthorizationMetadata>(A2AJsonUtilities.DefaultOptions)
                ?? throw new InvalidOperationException("The in-task authorization metadata is invalid.");
            if (!handledAuthorizationRequests.Add(authorization.AuthorizationRequest.Id))
            {
                throw new InvalidOperationException(
                    $"The authorization request '{authorization.AuthorizationRequest.Id}' was repeated.");
            }
            A2AAgentCardAuthentication authentication = A2AAgentCardAuthentication.CreateInTask(
                authorization.AuthorizationRequest.OAuth2.Flows,
                authorization.AuthorizationRequest.RequiredScopes);
            string token = await _accessTokenProvider.GetAccessTokenAsync(authentication, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The OAuth flow did not return an access token.");
            var resumeRequest = new ResumeAuthRequest
            {
                TaskId = task.Id,
                ContextId = task.ContextId,
                AuthorizationRequestId = authorization.AuthorizationRequest.Id,
            };

            using var request = _isJsonRpc
                ? CreateJsonRpcRequest(resumeRequest, token)
                : CreateHttpJsonRequest(resumeRequest, token);
            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using JsonDocument document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            JsonElement result = document.RootElement.TryGetProperty("result", out JsonElement jsonRpcResult)
                ? jsonRpcResult
                : document.RootElement;
            task = result.Deserialize<AgentTask>(A2AJsonUtilities.DefaultOptions)
                ?? throw new InvalidOperationException("The resumeAuth response did not contain an A2A task.");
        }

        return task;
    }

    private HttpRequestMessage CreateJsonRpcRequest(ResumeAuthRequest request, string token)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, _interfaceUri)
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                id = Guid.NewGuid().ToString("N"),
                method = "resumeAuth",
                @params = request,
            }, options: A2AJsonUtilities.DefaultOptions),
        };
        AddHeaders(message, token);
        return message;
    }

    private HttpRequestMessage CreateHttpJsonRequest(ResumeAuthRequest request, string token)
    {
        var baseUri = _interfaceUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? _interfaceUri
            : new Uri($"{_interfaceUri.AbsoluteUri}/", UriKind.Absolute);
        var requestUri = new Uri(baseUri, $"tasks/{Uri.EscapeDataString(request.TaskId)}:resumeAuth");
        A2AAgentOrigin.EnsureCredentialTarget(A2AAgentOrigin.FromAgentUrl(_interfaceUri), requestUri);
        var message = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(request, options: A2AJsonUtilities.DefaultOptions),
        };
        AddHeaders(message, token);
        return message;
    }

    private static void AddHeaders(HttpRequestMessage request, string token)
    {
        request.Headers.TryAddWithoutValidation(ExtensionHeader, ExtensionUri);
        request.Headers.TryAddWithoutValidation(TokenHeader, token);
    }

    private sealed class InTaskAuthorizationMetadata
    {
        [JsonPropertyName("authorizationRequest")]
        public AuthorizationRequest AuthorizationRequest { get; init; } = new();
    }

    private sealed class AuthorizationRequest
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("oauth2")]
        public OAuth2Authorization OAuth2 { get; init; } = new();

        [JsonPropertyName("requiredScopes")]
        public string[] RequiredScopes { get; init; } = [];
    }

    private sealed class OAuth2Authorization
    {
        [JsonPropertyName("flows")]
        public OAuthFlows Flows { get; init; } = new();
    }

    private sealed class ResumeAuthRequest
    {
        [JsonPropertyName("taskId")]
        public string TaskId { get; init; } = string.Empty;

        [JsonPropertyName("contextId")]
        public string ContextId { get; init; } = string.Empty;

        [JsonPropertyName("authorizationRequestId")]
        public string AuthorizationRequestId { get; init; } = string.Empty;
    }
}
