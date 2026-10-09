// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.CopilotStudio.Client.Discovery
{
    internal static class ConversationEndpointDiscovery
    {
        internal static async Task<(Uri OperationUri, Uri DirectConnectUri)> ResolveAsync(
            Uri discoveryUri,
            string operation,
            string? conversationId,
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync,
            CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, discoveryUri);
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.UserAgent.ParseAdd(UserAgentHelper.UserAgentHeader);
            using var response = await sendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
#if !NETSTANDARD
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
#endif
            var endpoints = ProtocolJsonSerializer.ToObject<ConversationEndpoints>(json)
                ?? throw new JsonException("Conversation endpoint discovery returned an empty response.");
            endpoints.Validate(discoveryUri);
            return (endpoints.GetUri(operation, conversationId), new Uri(endpoints.CreateConversation!));
        }

        private sealed class ConversationEndpoints
        {
            [JsonPropertyName("createConversation")]
            public string? CreateConversation { get; set; }

            [JsonPropertyName("executeTurn")]
            public string? ExecuteTurn { get; set; }

            [JsonPropertyName("continueTurn")]
            public string? ContinueTurn { get; set; }

            [JsonPropertyName("subscribe")]
            public string? Subscribe { get; set; }

            internal void Validate(Uri discoveryUri)
            {
                ValidateUrl(CreateConversation, discoveryUri, false);
                ValidateUrl(ExecuteTurn, discoveryUri, true);
                ValidateUrl(ContinueTurn, discoveryUri, true);
                if (Subscribe != null)
                {
                    ValidateUrl(Subscribe, discoveryUri, true);
                }
            }

            private static void ValidateUrl(string? url, Uri discoveryUri, bool requiresConversationId)
            {
                var suffixIndex = discoveryUri.Host.IndexOf(".environment.", StringComparison.OrdinalIgnoreCase);
                var suffix = suffixIndex >= 0 ? discoveryUri.Host.Substring(suffixIndex) : "." + discoveryUri.Host;
                if (url == null || string.IsNullOrWhiteSpace(url) ||
                    requiresConversationId != url.Contains("{conversationId}") ||
                    #if NET8_0_OR_GREATER
                    url.Replace("{conversationId}", string.Empty).Contains('{') ||
                    url.Replace("{conversationId}", string.Empty).Contains('}') ||
#else
                    url.Replace("{conversationId}", string.Empty).Contains("{") ||
                    url.Replace("{conversationId}", string.Empty).Contains("}") ||
#endif
                    !Uri.TryCreate(url.Replace("{conversationId}", "conversation"), UriKind.Absolute, out var uri) ||
                    uri.Scheme != Uri.UriSchemeHttps || uri.Port != discoveryUri.Port ||
                    !uri.Host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ||
                    !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment))
                {
                    throw new JsonException("Conversation endpoint discovery returned an invalid operation URL.");
                }
            }

            internal Uri GetUri(string operation, string? conversationId)
            {
                var url = operation switch
                {
                    "createConversation" => CreateConversation,
                    "executeTurn" => ExecuteTurn,
                    "subscribe" => Subscribe ?? throw new NotSupportedException("The agent does not advertise a subscribe endpoint."),
                    _ => throw new ArgumentException("Unknown conversation operation.", nameof(operation))
                };
                if (operation != "createConversation" && string.IsNullOrEmpty(conversationId))
                {
                    throw new ArgumentException("A conversation ID is required for this operation.", nameof(conversationId));
                }
                return new Uri(url!.Replace("{conversationId}", Uri.EscapeDataString(conversationId ?? string.Empty)));
            }
        }
    }
}
