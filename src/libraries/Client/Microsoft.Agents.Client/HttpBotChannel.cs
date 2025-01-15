// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Agents.Client
{
    /// <summary>
    /// Sends Activities to a channel using the Request-Request Activity Protocol semantics. Responses from the bot are
    /// HTTP requests to the caller via the supplied ServiceUrl on the Activity.
    /// </summary>
    /// <param name="tokenAccess"></param>
    /// <param name="httpClientFactory"></param>
    /// <param name="logger"></param>
    internal class HttpBotChannel(
        IAccessTokenProvider tokenAccess,
        IHttpClientFactory httpClientFactory,
        ILogger logger) : IChannel
    {
        private readonly IAccessTokenProvider _tokenAccess = tokenAccess;
        private readonly HttpClient _httpClient = httpClientFactory?.CreateClient() ?? new HttpClient();
        private readonly ILogger _logger = logger ?? NullLogger.Instance;
        private bool _disposed;

        /// <inheritdoc />
        public async Task<InvokeResponse> PostActivityAsync(string toBotId, string toBotResource, Uri endpoint, Uri serviceUrl, string conversationId, IActivity activity, CancellationToken cancellationToken = default)
        {
            return await PostActivityAsync<object>(toBotId, toBotResource, endpoint, serviceUrl, conversationId, activity, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<InvokeResponse<T>> PostActivityAsync<T>(string toBotId, string toBotResource, Uri endpoint, Uri serviceUrl, string conversationId, IActivity activity, CancellationToken cancellationToken = default)
        {
            toBotId = toBotId ?? string.Empty;
            _ = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            _ = serviceUrl ?? throw new ArgumentNullException(nameof(serviceUrl));
            _ = conversationId ?? throw new ArgumentNullException(nameof(conversationId));
            _ = activity ?? throw new ArgumentNullException(nameof(activity));

            _logger.LogInformation($"post to bot '{toBotId}' at '{endpoint}'");

            // Clone the activity so we can modify it before sending without impacting the original object.
            var activityClone = activity.Clone();

            // Apply the appropriate addressing to the newly created Activity.
            activityClone.RelatesTo = new ConversationReference
            {
                ServiceUrl = activityClone.ServiceUrl,
                ActivityId = activityClone.Id,
                ChannelId = activityClone.ChannelId,
                Locale = activityClone.Locale,
                Conversation = new ConversationAccount
                {
                    Id = activityClone.Conversation.Id,
                    Name = activityClone.Conversation.Name,
                    ConversationType = activityClone.Conversation.ConversationType,
                    AadObjectId = activityClone.Conversation.AadObjectId,
                    IsGroup = activityClone.Conversation.IsGroup,
                    Properties = activityClone.Conversation.Properties,
                    Role = activityClone.Conversation.Role,
                    TenantId = activityClone.Conversation.TenantId,
                }
            };
            activityClone.Conversation.Id = conversationId;
            activityClone.ServiceUrl = serviceUrl.ToString();
            activityClone.Recipient ??= new ChannelAccount();
            activityClone.Recipient.Role = RoleTypes.Skill;

            // Create the HTTP request from the cloned Activity and send it to the bot.
            using (var jsonContent = new StringContent(activityClone.ToJson(), Encoding.UTF8, "application/json"))
            {
                using (var httpRequestMessage = new HttpRequestMessage())
                {
                    httpRequestMessage.Method = HttpMethod.Post;
                    httpRequestMessage.RequestUri = endpoint;
                    httpRequestMessage.Content = jsonContent;

                    httpRequestMessage.Headers.Add(ConversationConstants.ConversationIdHttpHeaderName, conversationId);

                    // Add the auth header to the HTTP request.
                    var tokenResult = await _tokenAccess.GetAccessTokenAsync(toBotResource, [$"{toBotId}/.default"]).ConfigureAwait(false);
                    httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult);

                    using (var httpResponseMessage = await _httpClient.SendAsync(httpRequestMessage, cancellationToken).ConfigureAwait(false))
                    {
                        var content = httpResponseMessage.Content != null ? await httpResponseMessage.Content.ReadAsStringAsync().ConfigureAwait(false) : null;

                        if (httpResponseMessage.IsSuccessStatusCode)
                        {
                            // On success assuming either JSON that can be deserialized to T or empty.
                            return new InvokeResponse<T>
                            {
                                Status = (int)httpResponseMessage.StatusCode,
                                Body = content?.Length > 0 ? ProtocolJsonSerializer.ToObject<T>(content) : default
                            };
                        }
                        else
                        {
                            // Otherwise we can assume we don't have a T to deserialize - so just log the content so it's not lost.
                            _logger.LogError($"Bot request failed to '{endpoint}' returning '{(int)httpResponseMessage.StatusCode}' and '{content}'");

                            // We want to at least propagate the status code because that is what InvokeResponse expects.
                            return new InvokeResponse<T>
                            {
                                Status = (int)httpResponseMessage.StatusCode,
                                Body = typeof(T) == typeof(object) ? (T)(object)content : default,
                            };
                        }
                    }
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);

            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected implementation of dispose pattern.
        /// </summary>
        /// <param name="disposing">Indicates where this method is called from.</param>
        protected void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _httpClient.Dispose();
            _disposed = true;
        }
    }
}
