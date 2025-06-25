// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Builder;
using System.Text;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using System.Linq;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    /// <summary>
    /// The <see cref="CloudAdapter"/>will queue the incoming request to be 
    /// processed by the configured background service if possible.
    /// </summary>
    /// <remarks>
    /// Invoke and ExpectReplies are always handled synchronously.
    /// </remarks>
    public class CloudAdapter
        : ChannelServiceAdapterBase, IAgentHttpAdapter
    {
        private readonly IActivityTaskQueue _activityTaskQueue;
        private readonly AdapterOptions _adapterOptions;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="channelServiceClientFactory"></param>
        /// <param name="activityTaskQueue"></param>
        /// <param name="logger"></param>
        /// <param name="options">Defaults to Async enabled and 60 second shutdown delay timeout</param>
        /// <param name="middlewares"></param>
        /// <param name="config"></param>
        /// <exception cref="System.ArgumentNullException"></exception>
        public CloudAdapter(
            IChannelServiceClientFactory channelServiceClientFactory,
            IActivityTaskQueue activityTaskQueue,
            ILogger<IAgentHttpAdapter> logger = null,
            AdapterOptions options = null,
            Builder.IMiddleware[] middlewares = null,
            IConfiguration config = null)
            : base(channelServiceClientFactory, logger)
        {
            _activityTaskQueue = activityTaskQueue ?? throw new ArgumentNullException(nameof(activityTaskQueue));
            _adapterOptions = options ?? new AdapterOptions();

            if (middlewares != null)
            {
                foreach (var middleware in middlewares)
                {
                    Use(middleware);
                }
            }

            OnTurnError = async (turnContext, exception) =>
            {
                // Log any leaked exception from the application.
                StringBuilder sbError = new StringBuilder(1024);
                int iLevel = 0;
                bool emitStackTrace = true;
                if (config != null && config["EmitStackTrace"] != null)
                {
                    if (!bool.TryParse(config["EmitStackTrace"], out emitStackTrace))
                    {
                        emitStackTrace = true; // Default to true if parsing fails
                    }
                }
                StringBuilder lastErrorMessage = new StringBuilder(1024);
                exception.GetExceptionDetail(sbError, iLevel, lastErrorMsg: lastErrorMessage, includeStackTrace: emitStackTrace); // ExceptionParser
                if (exception is ErrorResponseException errorResponse && errorResponse.Body != null)
                {
                    sbError.Append(Environment.NewLine);
                    sbError.Append(errorResponse.Body.ToString());
                }
                string resolvedErrorMessage = sbError.ToString();

                // Writing formatted exception message to log with error codes and help links. 
#pragma warning disable CA2254 // Template should be a static expression
                logger.LogError(resolvedErrorMessage);
#pragma warning restore CA2254 // Template should be a static expression

                if (exception is not OperationCanceledException) // Do not try to send another message if the response has been canceled.
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text(lastErrorMessage.ToString()), CancellationToken.None);
                    // Send a trace activity
                    await turnContext.TraceActivityAsync("OnTurnError Trace", resolvedErrorMessage, "https://www.botframework.com/schemas/error", "TurnError");
                }
                sbError.Clear();
            };
        }

        /// <summary>
        /// This method can be called from inside a POST method on any Controller implementation.  If the activity is Not an Invoke, and
        /// DeliveryMode is Not ExpectReplies, and this is not a GET request to upgrade to WebSockets, then the activity will be enqueued
        /// for processing on a background thread.
        /// </summary>
        /// <remarks>
        /// Note, this is an ImmediateAccept and BackgroundProcessing override of: 
        /// Task IAgentHttpAdapter.ProcessAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, CancellationToken cancellationToken = default);
        /// </remarks>
        /// <param name="httpRequest">The HTTP request object, typically in a POST handler by a Controller.</param>
        /// <param name="httpResponse">The HTTP response object.</param>
        /// <param name="agent">The bot implementation.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive
        ///     notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        public async Task ProcessAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(httpRequest);
            ArgumentNullException.ThrowIfNull(httpResponse);
            ArgumentNullException.ThrowIfNull(agent);

            if (httpRequest.Method != HttpMethods.Post)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            }
            else
            {
                // Deserialize the incoming Activity
                var activity = await HttpHelper.ReadRequestAsync<IActivity>(httpRequest).ConfigureAwait(false);

                // If Auth is not configured, we still need the claims from the JWT token.
                // Currently, the stack does rely on certain Claims.  If the Bearer token
                // was sent, we can get them from there.  The JWT token is NOT validated though.
                var claimsIdentity = (ClaimsIdentity)httpRequest.HttpContext.User.Identity;
                if (!claimsIdentity.IsAuthenticated && !claimsIdentity.Claims.Any())
                {
                    var auth = httpRequest.Headers.Authorization;
                    if (auth.Count != 0)
                    {
                        var authHeaderValue = auth.First();
                        var authValues = authHeaderValue.Split(' ');
                        if (authValues.Length == 2 && authValues[0].Equals("bearer", StringComparison.OrdinalIgnoreCase))
                        {
                            var jwt = new JwtSecurityToken(authValues[1]);
                            claimsIdentity = new ClaimsIdentity(jwt.Claims);
                        }
                    }
                }

                if (!IsValidChannelActivity(activity))
                {
                    httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }

                try
                {
                    if (activity.Type == ActivityTypes.Invoke || activity.DeliveryMode == DeliveryModes.Stream || activity.DeliveryMode == DeliveryModes.ExpectReplies)
                    {
                        InvokeResponse invokeResponse = null;

                        IChannelResponseWriter writer = activity.DeliveryMode == DeliveryModes.Stream
                            ? new ActivityStreamedResponseWriter()
                            : new ExpectRepliesResponseWriter(activity);

                        await writer.ResponseBegin(httpResponse, cancellationToken).ConfigureAwait(false);

                        // Queue the activity to be processed by the ActivityBackgroundService, and stop ChannelResponseQueue when the
                        // turn is done.
                        _activityTaskQueue.QueueBackgroundActivity(claimsIdentity, activity, onComplete: (response) =>
                        {
                            ChannelResponseQueue.CompleteHandlerForConversation(activity.Conversation.Id);
                            invokeResponse = response;
                        });

                        // block until turn is complete
                        await ChannelResponseQueue.HandleResponsesAsync(activity.Conversation.Id, async (activity) =>
                        {
                            await writer.WriteActivity(httpResponse, activity, cancellationToken).ConfigureAwait(false);
                        }, cancellationToken).ConfigureAwait(false);

                        await writer.ResponseEnd(httpResponse, invokeResponse, cancellationToken: cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        // Queue the activity to be processed by the ActivityBackgroundService.  There is no response body in
                        // this case and the request is handled in the background.
                        _activityTaskQueue.QueueBackgroundActivity(claimsIdentity, activity);

                        // Activity has been queued to process, so return immediately
                        httpResponse.StatusCode = (int)HttpStatusCode.Accepted;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // handle unauthorized here as this layer creates the http response
                    httpResponse.StatusCode = (int)HttpStatusCode.Unauthorized;
                }
            }
        }

        /// <summary>
        /// CloudAdapter handles this override asynchronously if the Activity uses DeliverModes.Normal.  Otherwise
        /// as <see cref="ProcessActivityAsync(ClaimsIdentity, IActivity, AgentCallbackHandler, CancellationToken)"/> using
        /// `agent.OnTurnAsync`.
        /// </summary>
        /// <param name="claimsIdentity"></param>
        /// <param name="continuationActivity"></param>
        /// <param name="agent"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="audience"></param>
        public override Task ProcessProactiveAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, IAgent agent, CancellationToken cancellationToken, string audience = null)
        {
            // DeliveryModes.Normal can be pushed to the Queue which allows the calling request to continue without blocking.
            if (continuationActivity.DeliveryMode == null || continuationActivity.DeliveryMode == DeliveryModes.Normal)
            {
                // Queue the activity to be processed by the ActivityBackgroundService
                _activityTaskQueue.QueueBackgroundActivity(claimsIdentity, continuationActivity, proactive: true, proactiveAudience: audience);
                return Task.CompletedTask;
            }

            return base.ProcessProactiveAsync(claimsIdentity, continuationActivity, agent, cancellationToken, audience);
        }

        protected override async Task<bool> ChannelResponseAsync(IActivity incomingActivity, IActivity outActivity, CancellationToken cancellationToken)
        {
            // CloudAdapter handles Stream and ExpectReplies.  According to spec, any other values are handled as Normal.
            if (incomingActivity.DeliveryMode != DeliveryModes.Stream && incomingActivity.DeliveryMode != DeliveryModes.ExpectReplies)
            {
                return false;
            }

            await ChannelResponseQueue.SendActivitiesAsync(incomingActivity.Conversation.Id, [outActivity], cancellationToken).ConfigureAwait(false);

            return true;
        }

        private bool IsValidChannelActivity(IActivity activity)
        {
            if (activity == null)
            {
                Logger.LogWarning("BadRequest: Missing activity");
                return false;
            }

            if (string.IsNullOrEmpty(activity.Type?.ToString()))
            {
                Logger.LogWarning("BadRequest: Missing activity type");
                return false;
            }

            if (string.IsNullOrEmpty(activity.Conversation?.Id))
            {
                Logger.LogWarning("BadRequest: Missing Conversation.Id");
                return false;
            }

            return true;
        }
    }
}
