// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.Connector.Types;
using System.Text;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    /// <summary>
    /// The <see cref="CloudAdapter"/>will queue the incoming request to be 
    /// processed by the configured background service if possible.
    /// </summary>
    /// <remarks>
    /// If the activity is not an Invoke, and DeliveryMode is not ExpectReplies, and this
    /// is not a GET request to upgrade to WebSockets, then the activity will be enqueued for processing
    /// on a background thread.
    /// </remarks>
    /// <remarks>
    /// Create an instance of <see cref="CloudAdapter"/>.
    /// </remarks>
    /// <param name="configuration"></param>
    /// <param name="logger"></param>
    /// <param name="activityTaskQueue"></param>
    /// <param name="channelServiceClientFactory"></param>
    public class CloudAdapter
        : ChannelServiceAdapterBase, IBotHttpAdapter
    {
        private readonly IActivityTaskQueue _activityTaskQueue;
        private readonly bool _async;

        public CloudAdapter(
            IChannelServiceClientFactory channelServiceClientFactory,
            IActivityTaskQueue activityTaskQueue,
            ILogger<IBotHttpAdapter> logger = null,
            bool async = true) : base(channelServiceClientFactory, logger)
        {
            _activityTaskQueue = activityTaskQueue ?? throw new ArgumentNullException(nameof(activityTaskQueue));
            _async = async;

            OnTurnError = async (turnContext, exception) =>
            {
                // Log any leaked exception from the application.
                StringBuilder sbError = new StringBuilder(1024);
                sbError.Append(exception.Message);
                if (exception is ErrorResponseException errorResponse && errorResponse.Body != null)
                {
                    sbError.Append(Environment.NewLine);
                    sbError.Append(errorResponse.Body.ToString());
                }
                string resolvedErrorMessage = sbError.ToString();
                logger.LogError(exception, "Exception caught : {ExceptionMessage}", resolvedErrorMessage);

                await turnContext.SendActivityAsync(MessageFactory.Text(resolvedErrorMessage));

                // Send a trace activity
                await turnContext.TraceActivityAsync("OnTurnError Trace", resolvedErrorMessage, "https://www.botframework.com/schemas/error", "TurnError");
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
        /// Task IBotHttpAdapter.ProcessAsync(HttpRequest httpRequest, HttpResponse httpResponse, IBot bot, CancellationToken cancellationToken = default);
        /// </remarks>
        /// <param name="httpRequest">The HTTP request object, typically in a POST handler by a Controller.</param>
        /// <param name="httpResponse">The HTTP response object.</param>
        /// <param name="bot">The bot implementation.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive
        ///     notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        public async Task ProcessAsync(HttpRequest httpRequest, HttpResponse httpResponse, IBot bot, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(httpRequest);
            ArgumentNullException.ThrowIfNull(httpResponse);
            ArgumentNullException.ThrowIfNull(bot);

            // Get is a socket exchange request, so should be processed by base CloudAdapter
            if (httpRequest.Method == HttpMethods.Get)
            {
                await ProcessBlockingAsync(httpRequest, httpResponse, bot, cancellationToken);
            }
            else
            {
                // Deserialize the incoming Activity
                var activity = await HttpHelper.ReadRequestAsync<Activity>(httpRequest).ConfigureAwait(false);

                if (!IsValidChannelActivity(activity, httpResponse))
                {
                    httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }

                if (!_async || activity.Type == ActivityTypes.Invoke || activity.DeliveryMode == DeliveryModes.ExpectReplies)
                {
                    // NOTE: Invoke and ExpectReplies cannot be performed async, the response must be written before the calling thread is released.
                    // Process the inbound activity with the bot
                    await ProcessBlockingAsync(httpRequest, httpResponse, bot, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    try
                    {
                        // Queue the activity to be processed by the ActivityBackgroundService
                        _activityTaskQueue.QueueBackgroundActivity((ClaimsIdentity) httpRequest.HttpContext.User.Identity, activity);

                        // Activity has been queued to process, so return Ok immediately
                        httpResponse.StatusCode = (int)HttpStatusCode.OK;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // handle unauthorized here as this layer creates the http response
                        httpResponse.StatusCode = (int)HttpStatusCode.Unauthorized;
                    }
                }
            }
        }

        /// <summary>
        /// Process the inbound HTTP request with the agent resulting in the outbound HTTP response. This method can be called directly from a Controller.
        /// If the HTTP method is a POST, the body will contain the <see cref="Activity"/> to process. 
        /// </summary>
        /// <param name="httpRequest">The <see cref="HttpRequest"/>.</param>
        /// <param name="httpResponse">The <see cref="HttpResponse"/>.</param>
        /// <param name="bot">The <see cref="IBot"/> implementation to use for this request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A <see cref="Task"/> that represents the work queued to execute.</returns>
        public virtual async Task ProcessBlockingAsync(HttpRequest httpRequest, HttpResponse httpResponse, IBot bot, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(httpRequest);
            ArgumentNullException.ThrowIfNull(httpResponse);
            ArgumentNullException.ThrowIfNull(bot);

            try
            {
                ClaimsIdentity claimsIdentity = httpRequest.HttpContext.User.Identity as ClaimsIdentity;

                // Only POST requests are handled
                if (httpRequest.Method == HttpMethods.Post)
                {
                    // Deserialize the incoming Activity
                    var activity = await HttpHelper.ReadRequestAsync<Activity>(httpRequest).ConfigureAwait(false);

                    // A request must contain an Activity body
                    if (string.IsNullOrEmpty(activity?.Type))
                    {
                        httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                        Logger.LogWarning("BadRequest: Missing activity or activity type.");
                        return;
                    }

                    // Process the inbound activity with the bot
                    var invokeResponse = await ProcessActivityAsync(claimsIdentity, activity, bot.OnTurnAsync, cancellationToken).ConfigureAwait(false);

                    // Write the response, potentially serializing the InvokeResponse
                    await HttpHelper.WriteResponseAsync(httpResponse, invokeResponse).ConfigureAwait(false);
                }
                else
                {
                    httpResponse.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                // handle unauthorized here as this layer creates the http response
                httpResponse.StatusCode = (int)HttpStatusCode.Unauthorized;

                Logger.LogError(ex, "Unauthorized: {ExceptionMessage}", ex.ToString());
            }
        }

        private bool IsValidChannelActivity(Activity activity, HttpResponse httpResponse)
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
