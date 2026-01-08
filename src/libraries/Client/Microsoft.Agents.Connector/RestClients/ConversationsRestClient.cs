// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using Microsoft.Agents.Connector.Errors;
using Microsoft.Agents.Connector.Types;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Models.Activities;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Microsoft.Agents.Connector.RestClients
{
    internal class ConversationsRestClient(IRestTransport transport) : IConversations
    {
        /// <summary>
        /// Gets the maximum allowed length for an APX conversation ID.
        /// </summary>
        public int MaxApxConversationIdLength { get; set; } = 150;

        private readonly IRestTransport _transport = transport ?? throw new ArgumentNullException(nameof(_transport));

        internal HttpRequestMessage CreateGetConversationsRequest(string continuationToken)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,

                RequestUri = new Uri(_transport.Endpoint.EnsureTrailingSlash(), $"v3/conversations")
                    .AppendQuery("continuationToken", continuationToken)
            };

            request.Headers.Add("Accept", "application/json");
            return request;
        }

        /// <inheritdoc/>
        public async Task<ConversationsResult> GetConversationsAsync(string continuationToken = null, CancellationToken cancellationToken = default)
        {
            using var message = CreateGetConversationsRequest(continuationToken);
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    {
#if !NETSTANDARD
                        return ProtocolJsonSerializer.ToObject<ConversationsResult>(httpResponse.Content.ReadAsStream(cancellationToken));
#else
                        return ProtocolJsonSerializer.ToObject<ConversationsResult>(httpResponse.Content.ReadAsStringAsync().Result);
#endif
                    }
                default:
                    {
                        throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.SendGetConversationsError, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
                    }
            }
        }

        internal HttpRequestMessage CreateCreateConversationRequest(ConversationParameters body)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(_transport.Endpoint.EnsureTrailingSlash(), $"v3/conversations")
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
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                case 201:
                case 202:
                    {
#if !NETSTANDARD
                        return ProtocolJsonSerializer.ToObject<ConversationResourceResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
#else
                        return ProtocolJsonSerializer.ToObject<ConversationResourceResponse>(httpResponse.Content.ReadAsStringAsync().Result);
#endif
                    }
                default:
                    {
                        throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.SendCreateConversationError, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
                    }
            }
        }

        internal HttpRequestMessage CreateSendToConversationRequest(string conversationId, IActivity body)
        {
            var convId = TruncateConversationId(conversationId, body);

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(_transport.Endpoint.EnsureTrailingSlash(), $"v3/conversations/{HttpUtility.UrlEncode(convId)}/activities")
            };
            request.Headers.Add("Accept", "application/json");
            if (body != null)
            {
                request.Content = new StringContent(ProtocolJsonSerializer.ToJson(body), System.Text.Encoding.UTF8, "application/json");
            }
            return request;
        }

        public async Task<ResourceResponse> SendToConversationAsync(IActivity activity, CancellationToken cancellationToken = default)
        {
            return await SendToConversationAsync(activity.Conversation.Id, activity, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<ResourceResponse> SendToConversationAsync(string conversationId, IActivity body = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(conversationId, nameof(conversationId));

            using var message = CreateSendToConversationRequest(conversationId, body);
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                case 201:
                case 202:
                    {
#if !NETSTANDARD
                        if (httpResponse.Content.ReadAsStream(cancellationToken).Length == 0)
                        {
                            return new ResourceResponse() { Id = string.Empty };
                        }
                        return ProtocolJsonSerializer.ToObject<ResourceResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
#else
                        if (httpResponse.Content.ReadAsStringAsync().Result.Length == 0)
                        {
                            return new ResourceResponse() { Id = string.Empty };
                        }
                        return ProtocolJsonSerializer.ToObject<ResourceResponse>(httpResponse.Content.ReadAsStringAsync().Result);
#endif
                    }
                default:
                    {
                        throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.SendSendConversationError, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
                    }
            }
        }

        internal HttpRequestMessage CreateSendConversationHistoryRequest(string conversationId, Transcript body)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(_transport.Endpoint.EnsureTrailingSlash(), $"v3/conversations/{conversationId}/activities/history")
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
            AssertionHelpers.ThrowIfNullOrEmpty(conversationId, nameof(conversationId));

            using var message = CreateSendConversationHistoryRequest(conversationId, body);
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                case 201:
                case 202:
                    {
#if !NETSTANDARD
                        return ProtocolJsonSerializer.ToObject<ResourceResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
#else
                        return ProtocolJsonSerializer.ToObject<ResourceResponse>(httpResponse.Content.ReadAsStringAsync().Result);
#endif
                    }
                default:
                    {
                        throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.SendConversationHistoryError, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
                    }
            }
        }

        internal HttpRequestMessage CreateUpdateActivityRequest(string conversationId, string activityId, IActivity body)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Put,
                RequestUri = new Uri(_transport.Endpoint.EnsureTrailingSlash(), $"v3/conversations/{conversationId}/activities/{activityId}")
            };
            request.Headers.Add("Accept", "application/json");
            if (body != null)
            {
                request.Content = new StringContent(ProtocolJsonSerializer.ToJson(body), System.Text.Encoding.UTF8, "application/json");
            }
            return request;
        }

        public async Task<ResourceResponse> UpdateActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
        {
            return await UpdateActivityAsync(activity.Conversation.Id, activity.Id, activity, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<ResourceResponse> UpdateActivityAsync(string conversationId, string activityId, IActivity body = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(conversationId, nameof(conversationId));
            AssertionHelpers.ThrowIfNullOrEmpty(activityId, nameof(activityId));

            using var message = CreateUpdateActivityRequest(conversationId, activityId, body);
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                case 201:
                case 202:
                    {
#if !NETSTANDARD
                        return ProtocolJsonSerializer.ToObject<ResourceResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
#else
                        return ProtocolJsonSerializer.ToObject<ResourceResponse>(httpResponse.Content.ReadAsStringAsync().Result);
#endif
                    }
                default:
                    {
                        throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.SendUpdateActivityError, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
                    }
            }
        }

        internal HttpRequestMessage CreateReplyToActivityRequest(string conversationId, string activityId, IActivity body)
        {
            var convId = TruncateConversationId(conversationId, body);

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(_transport.Endpoint.EnsureTrailingSlash(), $"v3/conversations/{HttpUtility.UrlEncode(convId)}/activities/{HttpUtility.UrlEncode(activityId)}")
            };
            request.Headers.Add("Accept", "application/json");
            if (body != null)
            {
                request.Content = new StringContent(ProtocolJsonSerializer.ToJson(body), System.Text.Encoding.UTF8, "application/json");
            }
            return request;
        }

        public async Task<ResourceResponse> ReplyToActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
        {
            return activity == null
                ? throw new ArgumentNullException(nameof(activity))
                : await ReplyToActivityAsync(activity.Conversation.Id, activity.ReplyToId, activity, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<ResourceResponse> ReplyToActivityAsync(string conversationId, string activityId, IActivity body = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(activityId, nameof(activityId));
            AssertionHelpers.ThrowIfNullOrEmpty(conversationId, nameof(conversationId));

            using var message = CreateReplyToActivityRequest(conversationId, activityId, body);
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                case 201:
                case 202:
                    {
#if !NETSTANDARD
                        // Teams is famous for not returning a response body for these.
                        if (httpResponse.Content.ReadAsStream(cancellationToken).Length == 0)
                        {
                            return new ResourceResponse() { Id = string.Empty };
                        }
                        return ProtocolJsonSerializer.ToObject<ResourceResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
#else
                        // Teams is famous for not returning a response body for these.
                        if (httpResponse.Content.ReadAsStringAsync().Result.Length == 0)
                        {
                            return new ResourceResponse() { Id = string.Empty };
                        }
                        return ProtocolJsonSerializer.ToObject<ResourceResponse>(httpResponse.Content.ReadAsStringAsync().Result);
#endif
                    }
                default:
                    {
                        throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.SendReplyToActivityError, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
                    }
            }
        }

        internal HttpRequestMessage CreateDeleteActivityRequest(string conversationId, string activityId)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Delete,
                RequestUri = new Uri(_transport.Endpoint.EnsureTrailingSlash(), $"v3/conversations/{conversationId}/activities/{activityId}")
            };
            request.Headers.Add("Accept", "application/json");
            return request;
        }

        /// <inheritdoc/>
        public async Task DeleteActivityAsync(string conversationId, string activityId, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(conversationId, nameof(conversationId));
            AssertionHelpers.ThrowIfNullOrEmpty(activityId, nameof(activityId));

            using var message = CreateDeleteActivityRequest(conversationId, activityId);
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                case 202:
                    return;
                default:
                    {
                        throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.SendDeleteActivityError, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
                    }
            }
        }

        internal HttpRequestMessage CreateGetConversationMembersRequest(string conversationId)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(_transport.Endpoint.EnsureTrailingSlash(), $"v3/conversations/{conversationId}/members")
            };
            request.Headers.Add("Accept", "application/json");
            return request;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ChannelAccount>> GetConversationMembersAsync(string conversationId, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(conversationId, nameof(conversationId));

            using var message = CreateGetConversationMembersRequest(conversationId);
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    {
#if !NETSTANDARD
                        return ProtocolJsonSerializer.ToObject<IReadOnlyList<ChannelAccount>>(httpResponse.Content.ReadAsStream(cancellationToken));
#else
                        return ProtocolJsonSerializer.ToObject<IReadOnlyList<ChannelAccount>>(httpResponse.Content.ReadAsStringAsync().Result);
#endif
                    }
                default:
                    {
                        throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.SendGetConversationMembersError, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
                    }
            }
        }

        internal HttpRequestMessage CreateGetConversationMemberRequest(string conversationId, string userId)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(_transport.Endpoint.EnsureTrailingSlash(), $"v3/conversations/{conversationId}/members/{userId}")
            };
            request.Headers.Add("Accept", "application/json");
            return request;
        }

        /// <inheritdoc/>
        public async Task<ChannelAccount> GetConversationMemberAsync(string userId, string conversationId, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(conversationId, nameof(conversationId));
            AssertionHelpers.ThrowIfNullOrEmpty(userId, nameof(userId));

            using var message = CreateGetConversationMemberRequest(conversationId, userId);
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    {
#if !NETSTANDARD
                        return ProtocolJsonSerializer.ToObject<ChannelAccount>(httpResponse.Content.ReadAsStream(cancellationToken));
#else
                        return ProtocolJsonSerializer.ToObject<ChannelAccount>(httpResponse.Content.ReadAsStringAsync().Result);
#endif
                    }
                default:
                    {
                        throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.SendGetConversationMemberError, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
                    }
            }
        }

        internal HttpRequestMessage CreateDeleteConversationMemberRequest(string conversationId, string memberId)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Delete,
                RequestUri = new Uri(_transport.Endpoint.EnsureTrailingSlash(), $"v3/conversations/{conversationId}/members/{memberId}")
            };
            request.Headers.Add("Accept", "application/json");
            return request;
        }

        /// <inheritdoc/>
        public async Task DeleteConversationMemberAsync(string conversationId, string memberId, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(conversationId, nameof(conversationId));
            AssertionHelpers.ThrowIfNullOrEmpty(memberId, nameof(memberId));

            using var message = CreateDeleteConversationMemberRequest(conversationId, memberId);
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                case 204:
                    return;
                default:
                    {
                        throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.SendDeleteConversationMemberError, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
                    }
            }
        }
        internal HttpRequestMessage CreateGetConversationPagedMembersRequest(string conversationId, int? pageSize, string continuationToken = default)
        {
            var request = new HttpRequestMessage();
            request.Method = HttpMethod.Get;

            request.RequestUri = new Uri(_transport.Endpoint.EnsureTrailingSlash(), $"v3/conversations/{conversationId}/pagedmembers")
                .AppendQuery("pageSize", pageSize.HasValue ? pageSize.Value.ToString() : null)
                .AppendQuery("continuationToken", continuationToken);

            request.Headers.Add("Accept", "application/json");
            return request;
        }

        /// <inheritdoc/>
        public async Task<PagedMembersResult> GetConversationPagedMembersAsync(string conversationId, int? pageSize = default(int?), string continuationToken = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(conversationId, nameof(conversationId));

            using var message = CreateGetConversationPagedMembersRequest(conversationId, pageSize, continuationToken);
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    {
#if !NETSTANDARD
                        return ProtocolJsonSerializer.ToObject<PagedMembersResult>(httpResponse.Content.ReadAsStream(cancellationToken));
#else
                        return ProtocolJsonSerializer.ToObject<PagedMembersResult>(httpResponse.Content.ReadAsStringAsync().Result);
#endif
                    }
                default:
                    {
                        throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.SendGetConversationPagedMembersError, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
                    }
            }
        }

        internal HttpRequestMessage CreateGetActivityMembersRequest(string conversationId, string activityId)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(_transport.Endpoint.EnsureTrailingSlash(), $"v3/conversations/{conversationId}/activities/{activityId}/members")
            };
            request.Headers.Add("Accept", "application/json");
            return request;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ChannelAccount>> GetActivityMembersAsync(string conversationId, string activityId, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(conversationId, nameof(conversationId));
            AssertionHelpers.ThrowIfNullOrEmpty(activityId, nameof(activityId));

            using var message = CreateGetActivityMembersRequest(conversationId, activityId);
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    {
#if !NETSTANDARD
                        return ProtocolJsonSerializer.ToObject<IReadOnlyList<ChannelAccount>>(httpResponse.Content.ReadAsStream(cancellationToken));
#else
                        return ProtocolJsonSerializer.ToObject<IReadOnlyList<ChannelAccount>>(httpResponse.Content.ReadAsStringAsync().Result);
#endif
                    }
                default:
                    {
                        throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.SendGetActivityMembersError, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
                    }
            }
        }

        internal HttpRequestMessage CreateUploadAttachmentRequest(string conversationId, AttachmentData body)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(_transport.Endpoint.EnsureTrailingSlash(), $"v3/conversations/{conversationId}/attachments")
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
            AssertionHelpers.ThrowIfNullOrEmpty(conversationId, nameof(conversationId));

            using var message = CreateUploadAttachmentRequest(conversationId, body);
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                case 201:
                case 202:
                    {
#if !NETSTANDARD
                        return ProtocolJsonSerializer.ToObject<ResourceResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
#else
                        return ProtocolJsonSerializer.ToObject<ResourceResponse>(httpResponse.Content.ReadAsStringAsync().Result);
#endif
                    }
                default:
                    {
                        throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.SendUploadAttachmentError, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
                    }
            }
        }

        private string TruncateConversationId(string conversationId, IActivity body)
        {
            string convId;

            // Truncate conversationId for Teams and Agentic roles to MaxApxConversationIdLength characters
            if ((body?.ChannelId?.Channel == Channels.Msteams ||
                body?.ChannelId?.Channel == Channels.Agents)
                && (body?.From?.Role == RoleTypes.AgenticIdentity
                || body?.From?.Role == RoleTypes.AgenticUser))
            {
                convId = conversationId.Length > MaxApxConversationIdLength ? conversationId[..MaxApxConversationIdLength] : conversationId;
            }
            else
                convId = conversationId;

            return convId;
        }
    }
}
