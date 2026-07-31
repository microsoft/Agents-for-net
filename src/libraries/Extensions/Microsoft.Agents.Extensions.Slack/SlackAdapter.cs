// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Slack.Api;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Slack
{
    /// <summary>
    /// An adapter that receives Slack traffic directly (Events API and Interactivity) and sends
    /// replies directly to Slack, bypassing Azure Bot Service.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Inbound: <see cref="ProcessAsync"/> verifies the Slack request signature, answers the
    /// <c>url_verification</c> handshake, de-duplicates retried events, converts the Slack payload to a
    /// <see cref="SlackActivity"/>, and runs it through the agent pipeline. Slack requires an
    /// acknowledgement within 3 seconds, so the turn is processed and a <c>200</c> is returned.
    /// </para>
    /// <para>
    /// Outbound: <see cref="SendActivitiesAsync"/> renders message activities back to Slack via
    /// <c>chat.postMessage</c> using the bot token from <see cref="SlackAdapterOptions"/>. Agents may
    /// still call the Slack Web API directly (e.g. via <see cref="SlackAgentExtension.CallAsync"/>);
    /// the inbound conversion stamps the configured bot token onto
    /// <see cref="SlackChannelData.ApiToken"/> so existing handlers keep working.
    /// </para>
    /// </remarks>
    public class SlackAdapter : ChannelAdapter, IAgentHttpAdapter
    {
        internal const string SlackServiceUrl = "https://slack.com";

        private static readonly TimeSpan DedupeRetention = TimeSpan.FromMinutes(10);
        private const int DedupeMaxEntries = 5000;

        private readonly SlackAdapterOptions _options;
        private readonly SlackApi _slackApi;
        private readonly ConcurrentDictionary<string, DateTimeOffset> _processedEvents = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="SlackAdapter"/> class.
        /// </summary>
        /// <param name="options">The Slack configuration (bot token, signing secret, bot user id).</param>
        /// <param name="httpClientFactory">Factory used to create the HTTP client for Slack Web API calls.</param>
        /// <param name="logger">Optional logger.</param>
        public SlackAdapter(SlackAdapterOptions options, IHttpClientFactory httpClientFactory, ILogger<SlackAdapter> logger = null)
            : base(logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _slackApi = new SlackApi(httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory)));

            OnTurnError = async (turnContext, exception) =>
            {
                Logger.LogError(exception, "[SlackAdapter] unhandled error during turn.");
                try
                {
                    await turnContext.SendActivityAsync("The agent encountered an error.", cancellationToken: CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception sendEx)
                {
                    Logger.LogError(sendEx, "[SlackAdapter] failed to send error message.");
                }
            };
        }

        /// <inheritdoc/>
        public async Task ProcessAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(httpRequest);
            ArgumentNullException.ThrowIfNull(httpResponse);
            ArgumentNullException.ThrowIfNull(agent);

            if (!HttpMethods.IsPost(httpRequest.Method))
            {
                httpResponse.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                return;
            }

            string body;
            using (var reader = new StreamReader(httpRequest.Body, Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            }

            if (!VerifySignature(httpRequest, body))
            {
                Logger.LogWarning("[SlackAdapter] request signature verification failed.");
                httpResponse.StatusCode = (int)HttpStatusCode.Unauthorized;
                return;
            }

            SlackActivity activity;
            try
            {
                if (IsFormUrlEncoded(httpRequest.ContentType))
                {
                    // Interactivity (block_actions, view_submission, ...) arrives form-encoded as payload=<json>.
                    var payloadJson = ExtractFormValue(body, "payload");
                    if (string.IsNullOrEmpty(payloadJson))
                    {
                        httpResponse.StatusCode = (int)HttpStatusCode.OK;
                        return;
                    }

                    activity = CreateActivityFromInteractivePayload(payloadJson);
                }
                else
                {
                    // Events API arrives as application/json.
                    using var doc = JsonDocument.Parse(body);
                    var type = doc.RootElement.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;

                    if (string.Equals(type, "url_verification", StringComparison.Ordinal))
                    {
                        var challenge = doc.RootElement.TryGetProperty("challenge", out var challengeElement) ? challengeElement.GetString() : string.Empty;
                        httpResponse.StatusCode = (int)HttpStatusCode.OK;
                        httpResponse.ContentType = "text/plain";
                        await httpResponse.WriteAsync(challenge ?? string.Empty, cancellationToken).ConfigureAwait(false);
                        return;
                    }

                    if (!string.Equals(type, "event_callback", StringComparison.Ordinal))
                    {
                        // app_rate_limited and any other envelope types are acknowledged without processing.
                        httpResponse.StatusCode = (int)HttpStatusCode.OK;
                        return;
                    }

                    var envelope = ProtocolJsonSerializer.ToObject<EventEnvelope>(body);
                    if (!ShouldProcess(envelope))
                    {
                        httpResponse.StatusCode = (int)HttpStatusCode.OK;
                        return;
                    }

                    activity = CreateActivityFromEvent(envelope);
                }
            }
            catch (JsonException ex)
            {
                Logger.LogWarning(ex, "[SlackAdapter] unable to parse inbound Slack payload.");
                httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            if (activity == null)
            {
                // Nothing actionable (e.g. the bot's own message); acknowledge so Slack does not retry.
                httpResponse.StatusCode = (int)HttpStatusCode.OK;
                return;
            }

            var claimsIdentity = new ClaimsIdentity();
            await ProcessActivityAsync(claimsIdentity, activity, agent.OnTurnAsync, cancellationToken).ConfigureAwait(false);

            httpResponse.StatusCode = (int)HttpStatusCode.OK;
        }

        /// <inheritdoc/>
        public override async Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(turnContext);
            ArgumentNullException.ThrowIfNull(activities);

            var responses = new ResourceResponse[activities.Length];
            var channelData = turnContext.Activity?.GetChannelData<SlackChannelData>();
            var conversationId = turnContext.Activity?.Conversation?.Id;

            for (var index = 0; index < activities.Length; index++)
            {
                var activity = activities[index];
                responses[index] = new ResourceResponse(activity.Id ?? string.Empty);

                if (!activity.IsType(ActivityTypes.Message) || string.IsNullOrEmpty(activity.Text))
                {
                    // Only text messages are rendered to Slack here; typing/trace/etc. are no-ops.
                    continue;
                }

                var channel = channelData?.Channel ?? SafeChannelFromConversationId(conversationId);
                var threadTs = channelData?.ThreadTs ?? SafeThreadTsFromConversationId(conversationId);

                if (string.IsNullOrEmpty(channel))
                {
                    Logger.LogWarning("[SlackAdapter] cannot send activity: no Slack channel could be resolved.");
                    continue;
                }

                var response = await _slackApi.CallAsync("chat.postMessage", new
                {
                    channel,
                    text = activity.Text.SlackEncode(),
                    thread_ts = threadTs,
                }, channelData?.ApiToken ?? _options.BotToken, cancellationToken).ConfigureAwait(false);

                responses[index] = new ResourceResponse(response.ts ?? string.Empty);
            }

            return responses;
        }

        /// <inheritdoc/>
        public override async Task<ResourceResponse> UpdateActivityAsync(ITurnContext turnContext, IActivity activity, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(turnContext);
            ArgumentNullException.ThrowIfNull(activity);

            var channelData = turnContext.Activity?.GetChannelData<SlackChannelData>();
            var channel = channelData?.Channel ?? SafeChannelFromConversationId(turnContext.Activity?.Conversation?.Id);

            var response = await _slackApi.CallAsync("chat.update", new
            {
                channel,
                ts = activity.Id,
                text = activity.Text.SlackEncode(),
            }, channelData?.ApiToken ?? _options.BotToken, cancellationToken).ConfigureAwait(false);

            return new ResourceResponse(response.ts ?? string.Empty);
        }

        /// <inheritdoc/>
        public override Task DeleteActivityAsync(ITurnContext turnContext, ConversationReference reference, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(turnContext);
            ArgumentNullException.ThrowIfNull(reference);

            var channelData = turnContext.Activity?.GetChannelData<SlackChannelData>();
            var channel = channelData?.Channel ?? SafeChannelFromConversationId(reference.Conversation?.Id);

            return _slackApi.CallAsync("chat.delete", new
            {
                channel,
                ts = reference.ActivityId,
            }, channelData?.ApiToken ?? _options.BotToken, cancellationToken);
        }

        /// <summary>
        /// Verifies the inbound request originates from Slack using the signing-secret HMAC scheme.
        /// See https://docs.slack.dev/authentication/verifying-requests-from-slack.
        /// </summary>
        internal bool VerifySignature(HttpRequest httpRequest, string body)
        {
            if (string.IsNullOrEmpty(_options.SigningSecret))
            {
                // No signing secret configured: verification is disabled (local development only).
                return true;
            }

            var signature = httpRequest.Headers["X-Slack-Signature"].ToString();
            var timestamp = httpRequest.Headers["X-Slack-Request-Timestamp"].ToString();

            if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(timestamp) || !long.TryParse(timestamp, out var requestUnixTime))
            {
                return false;
            }

            // Reject stale requests to mitigate replay attacks.
            var age = Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - requestUnixTime);
            if (age > _options.RequestMaxAgeSeconds)
            {
                return false;
            }

            var baseString = $"v0:{timestamp}:{body}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.SigningSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));

            var computed = new StringBuilder("v0=", 3 + (hash.Length * 2));
            foreach (var b in hash)
            {
                computed.Append(b.ToString("x2"));
            }

            var expectedBytes = Encoding.UTF8.GetBytes(computed.ToString());
            var actualBytes = Encoding.UTF8.GetBytes(signature);

            return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
        }

        /// <summary>
        /// Returns <see langword="true"/> if the event has not been seen before and should be processed.
        /// Slack retries deliveries, so events are de-duplicated by <c>event_id</c>.
        /// </summary>
        private bool ShouldProcess(EventEnvelope envelope)
        {
            var eventId = envelope?.event_id;
            if (string.IsNullOrEmpty(eventId))
            {
                return true;
            }

            PruneDedupe();

            return _processedEvents.TryAdd(eventId, DateTimeOffset.UtcNow);
        }

        private void PruneDedupe()
        {
            var cutoff = DateTimeOffset.UtcNow - DedupeRetention;
            foreach (var entry in _processedEvents)
            {
                if (entry.Value < cutoff)
                {
                    _processedEvents.TryRemove(entry.Key, out _);
                }
            }

            // Hard cap as a safety valve against unbounded growth under a burst.
            if (_processedEvents.Count > DedupeMaxEntries)
            {
                foreach (var entry in _processedEvents)
                {
                    _processedEvents.TryRemove(entry.Key, out _);
                    if (_processedEvents.Count <= DedupeMaxEntries)
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Converts a Slack Events API <c>event_callback</c> envelope to a <see cref="SlackActivity"/>.
        /// Returns <see langword="null"/> when the event should be ignored (e.g. the bot's own message).
        /// </summary>
        private SlackActivity CreateActivityFromEvent(EventEnvelope envelope)
        {
            var content = envelope.event_content;
            if (content == null)
            {
                return null;
            }

            // Ignore messages authored by the bot itself (or any bot subtype) to avoid reply loops.
            var botId = content.Get<string>("bot_id");
            if (!string.IsNullOrEmpty(botId)
                || (!string.IsNullOrEmpty(_options.BotUserId) && string.Equals(content.user, _options.BotUserId, StringComparison.Ordinal)))
            {
                return null;
            }

            var channel = content.channel;
            var threadTs = content.Get<string>("thread_ts") ?? content.ts;

            var channelData = new SlackChannelData
            {
                Envelope = envelope,
                ApiToken = _options.BotToken,
            };

            var activity = new SlackActivity
            {
                ChannelId = Channels.Slack,
                ServiceUrl = SlackServiceUrl,
                Id = envelope.event_id ?? content.ts,
                Timestamp = DateTimeOffset.UtcNow,
                From = new ChannelAccount(id: content.user),
                Recipient = new ChannelAccount(id: _options.BotUserId),
                Conversation = new ConversationAccount(
                    id: SlackHelpers.CreateConversationId(_options.BotUserId, envelope.team_id, channel, threadTs))
                {
                    IsGroup = !string.Equals(content.channel_type, "im", StringComparison.Ordinal),
                },
            };

            activity.ChannelData = channelData;

            if (string.Equals(content.type, "message", StringComparison.Ordinal) && string.IsNullOrEmpty(content.subtype))
            {
                activity.Type = ActivityTypes.Message;
                activity.Text = content.text.SlackDecode();
            }
            else
            {
                activity.Type = ActivityTypes.Event;
                activity.Name = content.type;
            }

            return activity;
        }

        /// <summary>
        /// Converts a Slack interactivity payload (block_actions, view_submission, ...) to an Event
        /// <see cref="SlackActivity"/>.
        /// </summary>
        private SlackActivity CreateActivityFromInteractivePayload(string payloadJson)
        {
            var payload = ProtocolJsonSerializer.ToObject<ActionPayload>(payloadJson);

            var channel = payload.channel;
            var user = payload.Get<string>("user.id");
            var threadTs = payload.Get<string>("message.thread_ts") ?? payload.Get<string>("message.ts");

            var channelData = new SlackChannelData
            {
                Payload = payload,
                ApiToken = _options.BotToken,
            };

            var activity = new SlackActivity
            {
                Type = ActivityTypes.Event,
                Name = payload.type,
                ChannelId = Channels.Slack,
                ServiceUrl = SlackServiceUrl,
                Id = Guid.NewGuid().ToString(),
                Timestamp = DateTimeOffset.UtcNow,
                From = new ChannelAccount(id: user),
                Recipient = new ChannelAccount(id: _options.BotUserId),
                Conversation = new ConversationAccount(
                    id: SlackHelpers.CreateConversationId(_options.BotUserId, payload.Get<string>("team.id"), channel, threadTs)),
            };

            activity.ChannelData = channelData;

            return activity;
        }

        private static string SafeChannelFromConversationId(string conversationId)
        {
            return string.IsNullOrEmpty(conversationId) ? null : SlackHelpers.SlackChannelIdFromConversationId(conversationId);
        }

        private static string SafeThreadTsFromConversationId(string conversationId)
        {
            return string.IsNullOrEmpty(conversationId) ? null : SlackHelpers.SlackThreadTsFromConversationId(conversationId);
        }

        private static bool IsFormUrlEncoded(string contentType)
        {
            return !string.IsNullOrEmpty(contentType)
                && contentType.Contains("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase);
        }

        private static string ExtractFormValue(string body, string key)
        {
            if (string.IsNullOrEmpty(body))
            {
                return null;
            }

            foreach (var pair in body.Split('&'))
            {
                var separator = pair.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                var name = pair.Substring(0, separator);
                if (string.Equals(name, key, StringComparison.Ordinal))
                {
                    return WebUtility.UrlDecode(pair.Substring(separator + 1));
                }
            }

            return null;
        }
    }
}
