// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Slack.Api;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
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
    /// acknowledgement within 3 seconds, so the turn is queued for background processing and a
    /// <c>200</c> is returned immediately.
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

        private readonly SlackAdapterOptions _options;
        private readonly SlackRequestValidator _requestValidator;
        private readonly SlackRequestParser _requestParser;
        private readonly SlackActivityConverter _activityConverter;
        private readonly SlackEventDeduplicator _eventDeduplicator;
        private readonly SlackApi _slackApi;
        private readonly IActivityTaskQueue _activityTaskQueue;

        /// <summary>
        /// Initializes a new instance of the <see cref="SlackAdapter"/> class.
        /// </summary>
        /// <param name="options">The Slack configuration (bot token, signing secret, bot user id).</param>
        /// <param name="httpClientFactory">Factory used to create the HTTP client for Slack Web API calls.</param>
        /// <param name="logger">Optional logger.</param>
        public SlackAdapter(
            SlackAdapterOptions options,
            IHttpClientFactory httpClientFactory,
            ILogger<SlackAdapter> logger = null)
            : this(options, httpClientFactory, logger, null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SlackAdapter"/> class.
        /// </summary>
        /// <param name="options">The Slack configuration (bot token, signing secret, bot user id).</param>
        /// <param name="httpClientFactory">Factory used to create the HTTP client for Slack Web API calls.</param>
        /// <param name="logger">Optional logger.</param>
        /// <param name="slackApiLogger">Optional logger for Slack Web API requests and responses.</param>
        public SlackAdapter(
            SlackAdapterOptions options,
            IHttpClientFactory httpClientFactory,
            ILogger<SlackAdapter> logger,
            ILogger<SlackApi> slackApiLogger)
            : this(options, httpClientFactory, logger, slackApiLogger, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SlackAdapter"/> class.
        /// </summary>
        /// <param name="options">The Slack configuration (bot token, signing secret, bot user id).</param>
        /// <param name="httpClientFactory">Factory used to create the HTTP client for Slack Web API calls.</param>
        /// <param name="logger">Optional logger.</param>
        /// <param name="slackApiLogger">Optional logger for Slack Web API requests and responses.</param>
        /// <param name="activityTaskQueue">Queue used to process the agent turn after acknowledging Slack.</param>
        public SlackAdapter(
            SlackAdapterOptions options,
            IHttpClientFactory httpClientFactory,
            ILogger<SlackAdapter> logger,
            ILogger<SlackApi> slackApiLogger,
            IActivityTaskQueue activityTaskQueue)
            : base(logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _requestValidator = new SlackRequestValidator(_options);
            _requestParser = new SlackRequestParser();
            _activityConverter = new SlackActivityConverter(_options);
            _eventDeduplicator = new SlackEventDeduplicator();
            _activityTaskQueue = activityTaskQueue;
            _slackApi = new SlackApi(
                httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory)),
                slackApiLogger);

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

            if (!_requestValidator.Verify(httpRequest, body))
            {
                Logger.LogWarning("[SlackAdapter] request signature verification failed.");
                httpResponse.StatusCode = (int)HttpStatusCode.Unauthorized;
                return;
            }

            var requestId = httpRequest.HttpContext.TraceIdentifier;
            ParsedSlackRequest parsed;
            string eventId = null;

            SlackActivity activity;
            try
            {
                parsed = _requestParser.Parse(body, httpRequest.ContentType);

                SlackLogSanitizer.ExecuteSafely(() =>
                {
                    if (Logger.IsEnabled(LogLevel.Debug))
                    {
                        SlackAdapterLog.LogPayloadReceived(
                            Logger,
                            requestId,
                            SlackLogSanitizer.SanitizeJson(parsed.PayloadJson));
                    }
                });

                if (parsed.Kind == SlackRequestKind.UrlVerification)
                {
                    httpResponse.StatusCode = (int)HttpStatusCode.OK;
                    httpResponse.ContentType = "text/plain";
                    await httpResponse.WriteAsync(parsed.Challenge ?? string.Empty, cancellationToken).ConfigureAwait(false);
                    return;
                }

                if (parsed.Kind == SlackRequestKind.Ignore)
                {
                    httpResponse.StatusCode = (int)HttpStatusCode.OK;
                    return;
                }

                if (parsed.Kind == SlackRequestKind.Event)
                {
                    eventId = parsed.EventEnvelope?.event_id;
                    if (!_eventDeduplicator.TryAccept(eventId))
                    {
                        httpResponse.StatusCode = (int)HttpStatusCode.OK;
                        return;
                    }

                }

                activity = _activityConverter.Convert(parsed, agent.GetType());
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

            SlackLogSanitizer.ExecuteSafely(() =>
            {
                if (Logger.IsEnabled(LogLevel.Debug))
                {
                    var sanitizedActivity = SlackLogSanitizer.SanitizeObject(activity);
                    SlackAdapterLog.LogActivityCreated(Logger, requestId, activity.Conversation?.Id ?? string.Empty, sanitizedActivity);
                }
            });

            var claimsIdentity = new ClaimsIdentity();
            activity.RequestId ??= requestId;

            if (_activityTaskQueue != null)
            {
                if (!_activityTaskQueue.QueueBackgroundActivity(
                    claimsIdentity,
                    this,
                    activity,
                    agentType: agent.GetType(),
                    headers: httpRequest.Headers))
                {
                    _eventDeduplicator.Remove(eventId);

                    Logger.LogWarning("[SlackAdapter] unable to queue activity because the host is shutting down.");
                    httpResponse.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                    return;
                }
            }
            else
            {
                await ProcessActivityAsync(claimsIdentity, activity, agent.OnTurnAsync, cancellationToken).ConfigureAwait(false);
            }

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
                var threadTs = string.IsNullOrEmpty(conversationId)
                    ? channelData?.ThreadTs
                    : SafeThreadTsFromConversationId(conversationId);

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

                SlackLogSanitizer.ExecuteSafely(() =>
                {
                    if (Logger.IsEnabled(LogLevel.Debug))
                    {
                        SlackAdapterLog.LogMessageSent(
                            Logger,
                            conversationId ?? string.Empty,
                            response.ts ?? string.Empty,
                            SlackLogSanitizer.SanitizeObject(activity));
                    }
                });

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

            SlackLogSanitizer.ExecuteSafely(() =>
            {
                if (Logger.IsEnabled(LogLevel.Debug))
                {
                    SlackAdapterLog.LogMessageUpdated(
                        Logger,
                        turnContext.Activity?.Conversation?.Id ?? string.Empty,
                        response.ts ?? string.Empty,
                        SlackLogSanitizer.SanitizeObject(activity));
                }
            });

            return new ResourceResponse(response.ts ?? string.Empty);
        }

        /// <inheritdoc/>
        public override async Task DeleteActivityAsync(ITurnContext turnContext, ConversationReference reference, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(turnContext);
            ArgumentNullException.ThrowIfNull(reference);

            var channelData = turnContext.Activity?.GetChannelData<SlackChannelData>();
            var channel = channelData?.Channel ?? SafeChannelFromConversationId(reference.Conversation?.Id);

            var response = await _slackApi.CallAsync("chat.delete", new
            {
                channel,
                ts = reference.ActivityId,
            }, channelData?.ApiToken ?? _options.BotToken, cancellationToken).ConfigureAwait(false);

            SlackLogSanitizer.ExecuteSafely(() =>
            {
                if (Logger.IsEnabled(LogLevel.Debug))
                {
                    SlackAdapterLog.LogMessageDeleted(
                        Logger,
                        reference.Conversation?.Id ?? string.Empty,
                        response.ts ?? reference.ActivityId ?? string.Empty,
                        SlackLogSanitizer.SanitizeObject(reference));
                }
            });
        }

        private static string SafeChannelFromConversationId(string conversationId)
        {
            return string.IsNullOrEmpty(conversationId) ? null : SlackHelpers.SlackChannelIdFromConversationId(conversationId);
        }

        private static string SafeThreadTsFromConversationId(string conversationId)
        {
            return string.IsNullOrEmpty(conversationId) ? null : SlackHelpers.SlackThreadTsFromConversationId(conversationId);
        }

    }
}
