// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Agents.Client
{
    /// <summary>
    /// Sends Activities to a remote Agent.
    /// </summary>
    /// <param name="channelInfo"></param>
    /// <param name="httpClient"></param>
    /// <param name="tokenProvider"></param>
    /// <param name="logger"></param>
    internal class HttpBotChannel(
        IChannelInfo channelInfo,
        HttpClient httpClient,
        IAccessTokenProvider tokenProvider,
        ILogger<HttpBotChannel> logger = null) : IChannel
    {
        private readonly IChannelInfo _channelInfo = channelInfo ?? throw new ArgumentNullException(nameof(channelInfo));
        private readonly HttpBotChannelSettings _settings = new(channelInfo);
        private readonly IAccessTokenProvider _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        private readonly ILogger _logger = logger ?? NullLogger<HttpBotChannel>.Instance;
        private bool _disposed;

        public string Alias => _channelInfo.Alias;

        public string DisplayName => _channelInfo.DisplayName;

        public async Task SendActivityAsync(string channelConversationId, IActivity activity, CancellationToken cancellationToken, IActivity relatesTo = null)
        {
            await SendActivityAsync<object>(channelConversationId, activity, cancellationToken, relatesTo).ConfigureAwait(false);
        }

        public async Task<InvokeResponse<T>> SendActivityAsync<T>(string channelConversationId, IActivity activity, CancellationToken cancellationToken, IActivity relatesTo = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(channelConversationId);
            ArgumentNullException.ThrowIfNull(activity);

            _logger.LogInformation($"post to bot '{_settings.ClientId}' at '{_settings.Endpoint.ToString()}'");

            // Clone the activity so we can modify it before sending without impacting the original object.
            var activityClone = CreateSendActivity(channelConversationId, activity, relatesTo);

            // Create the HTTP request from the cloned Activity and send it to the bot.
            using var response = await SendRequest(channelConversationId, activityClone, cancellationToken).ConfigureAwait(false);
            var content = response.Content != null ? await response.Content.ReadAsStringAsync().ConfigureAwait(false) : null;

            if (response.IsSuccessStatusCode)
            {
                // On success assuming either JSON that can be deserialized to T or empty.
                return new InvokeResponse<T>
                {
                    Status = (int)response.StatusCode,
                    Body = content?.Length > 0 ? ProtocolJsonSerializer.ToObject<T>(content) : default
                };
            }
            else
            {
                // Otherwise we can assume we don't have a T to deserialize - so just log the content so it's not lost.
                _logger.LogError($"Bot request failed to '{_settings.Endpoint.ToString()}' returning '{(int)response.StatusCode}' and '{content}'");

                // We want to at least propagate the status code because that is what InvokeResponse expects.
                return new InvokeResponse<T>
                {
                    Status = (int)response.StatusCode,
                    Body = typeof(T) == typeof(object) ? (T)(object)content : default,
                };
            }
        }

        public async Task<T> SendActivityForResultAsync<T>(string channelConversationId, IActivity activity, Action<IActivity> handler, CancellationToken cancellationToken, IActivity relatesTo = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(channelConversationId);
            ArgumentNullException.ThrowIfNull(activity);
            ArgumentNullException.ThrowIfNull(handler);

            await foreach (var received in SendActivityStreamedAsync(channelConversationId, activity, cancellationToken, relatesTo))
            {
                if (received is IActivity receivedActivity)
                {
                    if (receivedActivity.Type == ActivityTypes.EndOfConversation)
                    {
                        if (receivedActivity.Code != EndOfConversationCodes.CompletedSuccessfully)
                        {
                            throw new ChannelOperationException($"Unsuccessful EOC from Channel: {receivedActivity.Code}");
                        }
                        
                        return ProtocolJsonSerializer.ToObject<T>(receivedActivity.Value);
                    }

                    handler(receivedActivity);
                }
                else if (received is InvokeResponse invokeResponse)
                {
                    if (invokeResponse.Status >= 200 && invokeResponse.Status <= 299)
                    {
                        throw new ChannelOperationException($"Unsuccessful InvokeResponse from Channel: {invokeResponse.Status}");
                    }

                    if (activity.DeliveryMode == DeliveryModes.ExpectReplies)
                    {
                        var expectedReplies = ProtocolJsonSerializer.ToObject<ExpectedReplies>(invokeResponse.Body);
                        foreach (var reply in expectedReplies.Activities)
                        {
                            handler(reply);
                        }

                        return ProtocolJsonSerializer.ToObject<T>(expectedReplies.Body);
                    }
                    else
                    {
                        return ProtocolJsonSerializer.ToObject<T>(invokeResponse.Body);
                    }
                }
            }

            return default;
        }

        private async IAsyncEnumerable<object> SendActivityStreamedAsync(string channelConversationId, IActivity activity, [EnumeratorCancellation] CancellationToken cancellationToken, IActivity relatesTo = null)
        {
            var activityClone = CreateSendActivity(channelConversationId, activity, relatesTo);
            activityClone.DeliveryMode = DeliveryModes.Stream;

            // Create the HTTP request from the cloned Activity and send it to the bot.
            using var response = await SendRequest(channelConversationId, activityClone, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Error sending request: {Status}", response.StatusCode);
                if (response.Content != null)
                {
                    string error = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Error: {Error}", error);
                    throw new HttpRequestException($"Error sending request: {response.StatusCode}. {error}");
                }
                throw new HttpRequestException($"Error sending request: {response.StatusCode}");
            }

            // Read streamed response
            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using StreamReader sr = new(stream);
            string streamType = string.Empty;

            string line;
            while ((line = ReadLineSafe(sr)) != null)
            {
                if (line!.StartsWith("event:", StringComparison.InvariantCulture))
                {
                    streamType = line[7..];
                }
                else if (line.StartsWith("data:", StringComparison.InvariantCulture) && streamType == "activity")
                {
                    string jsonRaw = line[6..];
                    var inActivity = ProtocolJsonSerializer.ToObject<IActivity>(jsonRaw);
                    yield return inActivity;
                }
                else if (line.StartsWith("data:", StringComparison.InvariantCulture) && streamType == "invokeResponse")
                {
                    string jsonRaw = line[6..];
                    yield return ProtocolJsonSerializer.ToObject<InvokeResponse>(jsonRaw);
                }
                else
                {
                    _logger.LogWarning("Channel {ChannelInfoId}: Unexpected stream type {StreamType}, {LineValue}", streamType, _channelInfo.Alias, line.Trim());
                }
            }
        }

        private static string ReadLineSafe(StreamReader reader)
        {
            try
            {
                return reader.ReadLine();
            }
            catch (Exception)
            {
                // TBD:  Not sure how to resolve this yet.  It is because Readline will throw when the 
                // other end closes the stream.
                // (HttpIoException.HttpRequestError == HttpRequestError.ResponseEnded)
                return null;
            }
        }

        private async Task<HttpResponseMessage> SendRequest(string channelConversationId, IActivity activity, CancellationToken cancellationToken)
        {
            var jsonContent = new StringContent(activity.ToJson(), Encoding.UTF8, "application/json");
            var httpRequestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = _settings.Endpoint,
                Content = jsonContent
            };

            httpRequestMessage.Headers.Add(ConversationConstants.ConversationIdHttpHeaderName, channelConversationId);

            // Add the auth header to the HTTP request.
            var tokenResult = await _tokenProvider.GetAccessTokenAsync(_settings.ResourceUrl, [$"{_settings.ClientId}/.default"]).ConfigureAwait(false);
            httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult);

            var completionOption = activity.DeliveryMode == DeliveryModes.Stream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead;
            return await _httpClient.SendAsync(httpRequestMessage, completionOption, cancellationToken).ConfigureAwait(false);
        }

        private IActivity CreateSendActivity(string channelConversationId, IActivity activity, IActivity relatesTo)
        {
            // Clone the activity so we can modify it before sending without impacting the original object.
            var activityClone = activity.Clone();

            // Apply the appropriate addressing to the newly created Activity.
            if (relatesTo != null)
            {
                activityClone.RelatesTo = new ConversationReference
                {
                    ServiceUrl = relatesTo.ServiceUrl,
                    ActivityId = relatesTo.Id,
                    ChannelId = relatesTo.ChannelId,
                    Locale = relatesTo.Locale,
                    Conversation = new ConversationAccount
                    {
                        Id = relatesTo.Conversation.Id,
                        Name = relatesTo.Conversation.Name,
                        ConversationType = relatesTo.Conversation.ConversationType,
                        AadObjectId = relatesTo.Conversation.AadObjectId,
                        IsGroup = relatesTo.Conversation.IsGroup,
                        Properties = relatesTo.Conversation.Properties,
                        Role = relatesTo.Conversation.Role,
                        TenantId = relatesTo.Conversation.TenantId,
                    }
                };
            }

            activityClone.ServiceUrl = _settings.ServiceUrl;
            activityClone.Recipient ??= new ChannelAccount();
            activityClone.Recipient.Role = RoleTypes.Skill;

            activityClone.Conversation ??= new ConversationAccount();
            if (!string.IsNullOrEmpty(activityClone.Conversation.Id))
            {
                activityClone.Conversation.Id = channelConversationId;
            }

            return activityClone;
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
