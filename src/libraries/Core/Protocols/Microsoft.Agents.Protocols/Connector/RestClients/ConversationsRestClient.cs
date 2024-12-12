// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Protocols.Primitives;
using Microsoft.Agents.Protocols.Serializer;

namespace Microsoft.Agents.Protocols.Connector
{
    internal class ConversationsRestClient(HttpClient client, Uri endpoint) : IConversations
    {
        private readonly HttpClient _httpClient = client ?? throw new ArgumentNullException(nameof(client));
        private readonly Uri _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));

        internal HttpRequestMessage CreateGetConversationsRequest(string continuationToken)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,

                RequestUri = new Uri(endpoint, $"v3/conversations")
                    .AppendQuery("continuationToken", continuationToken)
            };

            request.Headers.Add("Accept", "application/json");
            return request;
        }

        /// <inheritdoc/>
        public async Task<ConversationsResult> GetConversationsAsync(string continuationToken = null, CancellationToken cancellationToken = default)
        {
            using var message = CreateGetConversationsRequest(continuationToken);
            using var httpResponse = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    {
                        return ProtocolJsonSerializer.ToObject<ConversationsResult>(httpResponse.Content.ReadAsStream(cancellationToken));
                    }
                default:
                    {
                        var ex = new ErrorResponseException($"GetConversations operation returned an invalid status code '{httpResponse.StatusCode}'");

                        try
                        {
                            ErrorResponse errorBody = ProtocolJsonSerializer.ToObject<ErrorResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
                            if (errorBody != null)
                            {
                                ex.Body = errorBody;
                            }
                        }
                        catch (JsonException)
                        {
                            // Ignore the exception
                        }

                        throw ex;
                    }
            }
        }

        internal HttpRequestMessage CreateCreateConversationRequest(ConversationParameters body)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(endpoint, $"v3/conversations")
            };
            request.Headers.Add("Accept", "application/json");
            if (body != null)
            {
                request.Content = new StringContent(ProtocolJsonSerializer.ToJson(body), System.Text.Encoding.UTF8, "application/json");
            }
            return request;
        }

        /// <inheritdoc/>
        public async Task<ConversationResourceResponse> CreateConversationAsync(ConversationParameters body = null, CancellationToken cancellationToken = default)
        {
            using var message = CreateCreateConversationRequest(body);
            using var httpResponse = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                case 201:
                case 202:
                    {
                        return ProtocolJsonSerializer.ToObject<ConversationResourceResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
                    }
                default:
                    {
                        var ex = new ErrorResponseException($"CreateConversation operation returned an invalid status code '{httpResponse.StatusCode}'");
                        try
                        {
                            ErrorResponse errorBody = ProtocolJsonSerializer.ToObject<ErrorResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
                            if (errorBody != null)
                            {
                                ex.Body = errorBody;
                            }
                        }
                        catch (JsonException)
                        {
                            // Ignore the exception
                        }
                        throw ex;
                    }
            }
        }

        internal HttpRequestMessage CreateSendToConversationRequest(string conversationId, IActivity body)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(endpoint, $"v3/conversations/{conversationId}/activities")
            };
            request.Headers.Add("Accept", "application/json");
            if (body != null)
            {
                request.Content = new StringContent(ProtocolJsonSerializer.ToJson(body), System.Text.Encoding.UTF8, "application/json");
            }
            return request;
        }

        public async Task<ResourceResponse> SendToConversationAsync(IActivity activity, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await SendToConversationAsync(activity.Conversation.Id, activity, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<ResourceResponse> SendToConversationAsync(string conversationId, IActivity body = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(conversationId);

            using var message = CreateSendToConversationRequest(conversationId, body);
            using var httpResponse = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                case 201:
                case 202:
                    {
                        return ProtocolJsonSerializer.ToObject<ResourceResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
                    }
                default:
                    {
                        var ex = new ErrorResponseException($"SendToConversation operation returned an invalid status code '{httpResponse.StatusCode}'");
                        try
                        {
                            ErrorResponse errorBody = ProtocolJsonSerializer.ToObject<ErrorResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
                            if (errorBody != null)
                            {
                                ex.Body = errorBody;
                            }
                        }
                        catch (JsonException)
                        {
                            // Ignore the exception
                        }
                        throw ex;
                    }
            }
        }

        internal HttpRequestMessage CreateSendConversationHistoryRequest(string conversationId, Transcript body)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(endpoint, $"v3/conversations/{conversationId}/activities/history")
            };
            request.Headers.Add("Accept", "application/json");
            if (body != null)
            {
                request.Content = new StringContent(ProtocolJsonSerializer.ToJson(body), System.Text.Encoding.UTF8, "application/json");
            }
            return request;
        }

        /// <inheritdoc/>
        public async Task<ResourceResponse> SendConversationHistoryAsync(string conversationId, Transcript body = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(conversationId);

            using var message = CreateSendConversationHistoryRequest(conversationId, body);
            using var httpResponse = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                case 201:
                case 202:
                    {
                        return ProtocolJsonSerializer.ToObject<ResourceResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
                    }
                default:
                    {
                        var ex = new ErrorResponseException($"SendConversationHistory operation returned an invalid status code '{httpResponse.StatusCode}'");
                        try
                        {
                            ErrorResponse errorBody = ProtocolJsonSerializer.ToObject<ErrorResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
                            if (errorBody != null)
                            {
                                ex.Body = errorBody;
                            }
                        }
                        catch (JsonException)
                        {
                            // Ignore the exception
                        }
                        throw ex;
                    }
            }
        }

        internal HttpRequestMessage CreateUpdateActivityRequest(string conversationId, string activityId, IActivity body)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Put,
                RequestUri = new Uri(endpoint, $"v3/conversations/{conversationId}/activities/{activityId}")
            };
            request.Headers.Add("Accept", "application/json");
            if (body != null)
            {
                request.Content = new StringContent(ProtocolJsonSerializer.ToJson(body), System.Text.Encoding.UTF8, "application/json");
            }
            return request;
        }

        public async Task<ResourceResponse> UpdateActivityAsync(IActivity activity, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await UpdateActivityAsync(activity.Conversation.Id, activity.Id, activity, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<ResourceResponse> UpdateActivityAsync(string conversationId, string activityId, IActivity body = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(conversationId);
            ArgumentNullException.ThrowIfNullOrEmpty(activityId);

            using var message = CreateUpdateActivityRequest(conversationId, activityId, body);
            using var httpResponse = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                case 201:
                case 202:
                    {
                        return ProtocolJsonSerializer.ToObject<ResourceResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
                    }
                default:
                    {
                        var ex = new ErrorResponseException($"UpdateActivity operation returned an invalid status code '{httpResponse.StatusCode}'");
                        try
                        {
                            ErrorResponse errorBody = ProtocolJsonSerializer.ToObject<ErrorResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
                            if (errorBody != null)
                            {
                                ex.Body = errorBody;
                            }
                        }
                        catch (JsonException)
                        {
                            // Ignore the exception
                        }
                        throw ex;
                    }
            }
        }

        internal HttpRequestMessage CreateReplyToActivityRequest(string conversationId, string activityId, IActivity body)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(endpoint, $"v3/conversations/{conversationId}/activities/{activityId}")
            };
            request.Headers.Add("Accept", "application/json");
            if (body != null)
            {
                request.Content = new StringContent(ProtocolJsonSerializer.ToJson(body), System.Text.Encoding.UTF8, "application/json");
            }
            return request;
        }

        public async Task<ResourceResponse> ReplyToActivityAsync(IActivity activity, CancellationToken cancellationToken = default(CancellationToken))
        {
            return activity == null
                ? throw new ArgumentNullException(nameof(activity))
                : await ReplyToActivityAsync(activity.Conversation.Id, activity.ReplyToId, activity, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<ResourceResponse> ReplyToActivityAsync(string conversationId, string activityId, IActivity body = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(conversationId);
            ArgumentNullException.ThrowIfNullOrEmpty(activityId);

            using var message = CreateReplyToActivityRequest(conversationId, activityId, body);
            using var httpResponse = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                case 201:
                case 202:
                    {
                        // Teams is famous for not returning a response body for these.
                        if (httpResponse.Content.ReadAsStream(cancellationToken).Length == 0)
                        {
                            return new ResourceResponse() { Id = string.Empty };
                        }
                        return ProtocolJsonSerializer.ToObject<ResourceResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
                    }
                default:
                    {
                        var ex = new ErrorResponseException($"ReplyToActivity operation returned an invalid status code '{httpResponse.StatusCode}'");
                        try
                        {
                            ErrorResponse errorBody = ProtocolJsonSerializer.ToObject<ErrorResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
                            if (errorBody != null)
                            {
                                ex.Body = errorBody;
                            }
                        }
                        catch (JsonException)
                        {
                            // Ignore the exception
                        }
                        throw ex;
                    }
            }
        }

        internal HttpRequestMessage CreateDeleteActivityRequest(string conversationId, string activityId)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Delete,
                RequestUri = new Uri(endpoint, $"v3/conversations/{conversationId}/activities/{activityId}")
            };
            request.Headers.Add("Accept", "application/json");
            return request;
        }

        /// <inheritdoc/>
        public async Task DeleteActivityAsync(string conversationId, string activityId, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(conversationId);
            ArgumentNullException.ThrowIfNullOrEmpty(activityId);

            using var message = CreateDeleteActivityRequest(conversationId, activityId);
            using var httpResponse = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                case 202:
                    return;
                default:
                    {
                        var ex = new ErrorResponseException($"DeleteActivity operation returned an invalid status code '{httpResponse.StatusCode}'");
                        try
                        {
                            ErrorResponse errorBody = ProtocolJsonSerializer.ToObject<ErrorResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
                            if (errorBody != null)
                            {
                                ex.Body = errorBody;
                            }
                        }
                        catch (JsonException)
                        {
                            // Ignore the exception
                        }
                        throw ex;
                    }
            }
        }

        internal HttpRequestMessage CreateGetConversationMembersRequest(string conversationId)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(endpoint, $"v3/conversations/{conversationId}/members")
            };
            request.Headers.Add("Accept", "application/json");
            return request;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ChannelAccount>> GetConversationMembersAsync(string conversationId, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(conversationId);

            using var message = CreateGetConversationMembersRequest(conversationId);
            using var httpResponse = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    {
                        return ProtocolJsonSerializer.ToObject<IReadOnlyList<ChannelAccount>>(httpResponse.Content.ReadAsStream(cancellationToken));
                    }
                default:
                    {
                        var ex = new ErrorResponseException($"GetConversationMembers operation returned an invalid status code '{httpResponse.StatusCode}'");
                        try
                        {
                            ErrorResponse errorBody = ProtocolJsonSerializer.ToObject<ErrorResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
                            if (errorBody != null)
                            {
                                ex.Body = errorBody;
                            }
                        }
                        catch (JsonException)
                        {
                            // Ignore the exception
                        }
                        throw ex;
                    }
            }
        }

        internal HttpRequestMessage CreateGetConversationMemberRequest(string conversationId, string userId)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(endpoint, $"v3/conversations/{conversationId}/members/{userId}")
            };
            request.Headers.Add("Accept", "application/json");
            return request;
        }

        /// <inheritdoc/>
        public async Task<ChannelAccount> GetConversationMemberAsync(string userId, string conversationId, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(conversationId);
            ArgumentNullException.ThrowIfNullOrEmpty(userId);

            using var message = CreateGetConversationMemberRequest(conversationId, userId);
            using var httpResponse = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    {
                        return ProtocolJsonSerializer.ToObject<ChannelAccount>(httpResponse.Content.ReadAsStream(cancellationToken));
                    }
                default:
                    {
                        var ex = new ErrorResponseException($"GetConversationMember operation returned an invalid status code '{httpResponse.StatusCode}'");
                        try
                        {
                            ErrorResponse errorBody = ProtocolJsonSerializer.ToObject<ErrorResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
                            if (errorBody != null)
                            {
                                ex.Body = errorBody;
                            }
                        }
                        catch (JsonException)
                        {
                            // Ignore the exception
                        }
                        throw ex;
                    }
            }
        }

        internal HttpRequestMessage CreateDeleteConversationMemberRequest(string conversationId, string memberId)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Delete,
                RequestUri = new Uri(endpoint, $"v3/conversations/{conversationId}/members/{memberId}")
            };
            request.Headers.Add("Accept", "application/json");
            return request;
        }

        /// <inheritdoc/>
        public async Task DeleteConversationMemberAsync(string conversationId, string memberId, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(conversationId);
            ArgumentNullException.ThrowIfNullOrEmpty(memberId);

            using var message = CreateDeleteConversationMemberRequest(conversationId, memberId);
            using var httpResponse = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                case 204:
                    return;
                default:
                    {
                        var ex = new ErrorResponseException($"DeleteConversationMember operation returned an invalid status code '{httpResponse.StatusCode}'");
                        try
                        {
                            ErrorResponse errorBody = ProtocolJsonSerializer.ToObject<ErrorResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
                            if (errorBody != null)
                            {
                                ex.Body = errorBody;
                            }
                        }
                        catch (JsonException)
                        {
                            // Ignore the exception
                        }
                        throw ex;
                    }
            }
        }
        internal HttpRequestMessage CreateGetConversationPagedMembersRequest(string conversationId, int? pageSize, string continuationToken)
        {
            var request = new HttpRequestMessage();
            request.Method = HttpMethod.Get;

            request.RequestUri = new Uri(endpoint, $"v3/conversations/{conversationId}/pagedmembers")
                .AppendQuery("pageSize", pageSize.Value.ToString())
                .AppendQuery("continuationToken", continuationToken);

            request.Headers.Add("Accept", "application/json");
            return request;
        }

        /// <inheritdoc/>
        public async Task<PagedMembersResult> GetConversationPagedMembersAsync(string conversationId, int? pageSize = null, string continuationToken = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(conversationId);

            using var message = CreateGetConversationPagedMembersRequest(conversationId, pageSize, continuationToken);
            using var httpResponse = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    {
                        return ProtocolJsonSerializer.ToObject<PagedMembersResult>(httpResponse.Content.ReadAsStream(cancellationToken));
                    }
                default:
                    {
                        var ex = new ErrorResponseException($"GetConversationPagedMembers operation returned an invalid status code '{httpResponse.StatusCode}'");
                        try
                        {
                            ErrorResponse errorBody = ProtocolJsonSerializer.ToObject<ErrorResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
                            if (errorBody != null)
                            {
                                ex.Body = errorBody;
                            }
                        }
                        catch (JsonException)
                        {
                            // Ignore the exception
                        }
                        throw ex;
                    }
            }
        }

        internal HttpRequestMessage CreateGetActivityMembersRequest(string conversationId, string activityId)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(endpoint, $"v3/conversations/{conversationId}/activities/{activityId}/members")
            };
            request.Headers.Add("Accept", "application/json");
            return request;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ChannelAccount>> GetActivityMembersAsync(string conversationId, string activityId, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(conversationId);
            ArgumentNullException.ThrowIfNullOrEmpty(activityId);

            using var message = CreateGetActivityMembersRequest(conversationId, activityId);
            using var httpResponse = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    {
                        return ProtocolJsonSerializer.ToObject<IReadOnlyList<ChannelAccount>>(httpResponse.Content.ReadAsStream(cancellationToken));
                    }
                default:
                    {
                        var ex = new ErrorResponseException($"GetActivityMembers operation returned an invalid status code '{httpResponse.StatusCode}'");
                        try
                        {
                            ErrorResponse errorBody = ProtocolJsonSerializer.ToObject<ErrorResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
                            if (errorBody != null)
                            {
                                ex.Body = errorBody;
                            }
                        }
                        catch (JsonException)
                        {
                            // Ignore the exception
                        }
                        throw ex;
                    }
            }
        }

        internal HttpRequestMessage CreateUploadAttachmentRequest(string conversationId, AttachmentData body)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(endpoint, $"v3/conversations/{conversationId}/attachments")
            };
            request.Headers.Add("Accept", "application/json");
            if (body != null)
            {
                request.Content = new StringContent(ProtocolJsonSerializer.ToJson(body), System.Text.Encoding.UTF8, "application/json");
            }
            return request;
        }

        /// <inheritdoc/>
        public async Task<ResourceResponse> UploadAttachmentAsync(string conversationId, AttachmentData body = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(conversationId);

            using var message = CreateUploadAttachmentRequest(conversationId, body);
            using var httpResponse = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                case 201:
                case 202:
                    {
                        return ProtocolJsonSerializer.ToObject<ResourceResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
                    }
                default:
                    {
                        var ex = new ErrorResponseException($"UploadAttachment operation returned an invalid status code '{httpResponse.StatusCode}'");
                        try
                        {
                            ErrorResponse errorBody = ProtocolJsonSerializer.ToObject<ErrorResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
                            if (errorBody != null)
                            {
                                ex.Body = errorBody;
                            }
                        }
                        catch (JsonException)
                        {
                            // Ignore the exception
                        }
                        throw ex;
                    }
            }
        }
    }
}
