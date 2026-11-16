// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using A2A.Grpc.Extensions.Protos;
using Google.Protobuf;
using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2ACli.A2A;

internal sealed class A2AInTaskAuthorizationClient
{
    internal const string ExtensionUri = "https://schemas.microsoft.com/agents/a2a/extensions/in-task-authorization/v1";
    internal const string ExtensionHeader = "A2A-Extensions";
    internal const string TokenHeader = "x-a2a-intask-authorization";
    private readonly HttpClient _httpClient;
    private readonly IA2AAccessTokenProvider _accessTokenProvider;
    private readonly Uri _interfaceUri;
    private readonly bool _isJsonRpc;
    private readonly A2AExtensionService.A2AExtensionServiceClient? _grpcClient;

    private A2AInTaskAuthorizationClient(
        HttpClient httpClient,
        IA2AAccessTokenProvider accessTokenProvider,
        A2AClientConnection connection)
    {
        _httpClient = httpClient;
        _accessTokenProvider = accessTokenProvider;
        _interfaceUri = connection.Address;
        _isJsonRpc = string.Equals(connection.ProtocolBinding, ProtocolBindingNames.JsonRpc, StringComparison.OrdinalIgnoreCase);
        _grpcClient = connection.Channel is null ? null : new A2AExtensionService.A2AExtensionServiceClient(connection.Channel);
    }

    internal static A2AInTaskAuthorizationClient Create(
        A2AClientConnection connection,
        HttpClient httpClient,
        IA2AAccessTokenProvider accessTokenProvider)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(accessTokenProvider);
        return new A2AInTaskAuthorizationClient(httpClient, accessTokenProvider, connection);
    }

    internal async Task<AgentTask?> ResumeIfRequiredAsync(AgentTask? task, CancellationToken cancellationToken)
    {
        var handledAuthorizationRequests = new HashSet<string>(StringComparer.Ordinal);

        while (task?.Status.State == TaskState.AuthRequired
            && task.Status.Message?.Metadata is not null
            && task.Status.Message.Metadata.TryGetValue(ExtensionUri, out JsonElement metadata))
        {
            A2AAgentOrigin.EnsureCredentialTarget(A2AAgentOrigin.FromAgentUrl(_interfaceUri), _interfaceUri);
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
            var authResponseRequest = new AuthResponseRequest
            {
                TaskId = task.Id,
                ContextId = task.ContextId,
                AuthorizationRequestId = authorization.AuthorizationRequest.Id,
            };

            if (_grpcClient is not null)
            {
                task = await SendGrpcAuthResponseAsync(authResponseRequest, token, cancellationToken).ConfigureAwait(false);
                continue;
            }

            using var request = _isJsonRpc
                ? CreateJsonRpcRequest(authResponseRequest, token)
                : CreateHttpJsonRequest(authResponseRequest, token);
            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using JsonDocument document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            JsonElement result = document.RootElement.TryGetProperty("result", out JsonElement jsonRpcResult)
                ? jsonRpcResult
                : document.RootElement;
            task = result.Deserialize<AgentTask>(A2AJsonUtilities.DefaultOptions)
                ?? throw new InvalidOperationException("The auth-response result did not contain an A2A task.");
        }

        return task;
    }

    private async Task<AgentTask> SendGrpcAuthResponseAsync(AuthResponseRequest request, string token, CancellationToken cancellationToken)
    {
        var headers = new Metadata
        {
            { "a2a-version", "1.0" },
            { "a2a-extensions", ExtensionUri },
            { TokenHeader, token },
        };
        using var call = _grpcClient!.InvokeExtensionOperationAsync(new ExtensionOperationRequest
        {
            OperationId = ExtensionUri + "#auth-response",
            Payload = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(request, A2AJsonUtilities.DefaultOptions)),
        }, headers, cancellationToken: cancellationToken);
        try
        {
            ExtensionOperationResponse response = await call.ResponseAsync.ConfigureAwait(false);
            return JsonSerializer.Deserialize<AgentTask>(response.Payload.Span, A2AJsonUtilities.DefaultOptions)
                ?? throw new InvalidOperationException("The auth-response result did not contain an A2A task.");
        }
        catch (RpcException exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("The gRPC auth-response call was canceled.", exception, cancellationToken);
        }
    }

    private HttpRequestMessage CreateJsonRpcRequest(AuthResponseRequest request, string token)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, _interfaceUri)
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                id = Guid.NewGuid().ToString("N"),
                method = "auth-response",
                @params = request,
            }, options: A2AJsonUtilities.DefaultOptions),
        };
        AddHeaders(message, token);
        return message;
    }

    private HttpRequestMessage CreateHttpJsonRequest(AuthResponseRequest request, string token)
    {
        var baseUri = _interfaceUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? _interfaceUri
            : new Uri($"{_interfaceUri.AbsoluteUri}/", UriKind.Absolute);
        var requestUri = new Uri(baseUri, $"tasks/{Uri.EscapeDataString(request.TaskId)}:auth-response");
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

    private sealed class AuthResponseRequest
    {
        [JsonPropertyName("taskId")]
        public string TaskId { get; init; } = string.Empty;

        [JsonPropertyName("contextId")]
        public string ContextId { get; init; } = string.Empty;

        [JsonPropertyName("authorizationRequestId")]
        public string AuthorizationRequestId { get; init; } = string.Empty;
    }
}
