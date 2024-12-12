// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Agents.Protocols.Primitives;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    /// <summary>
    /// The <see cref="AsyncCloudAdapter"/>will queue the incoming request to be 
    /// processed by the configured background service if possible.
    /// </summary>
    /// <remarks>
    /// If the activity is not an Invoke, and DeliveryMode is not ExpectReplies, and this
    /// is not a GET request to upgrade to WebSockets, then the activity will be enqueued for processing
    /// on a background thread.
    /// </remarks>
    /// <remarks>
    /// Create an instance of <see cref="AsyncCloudAdapter"/>.
    /// </remarks>
    /// <param name="configuration"></param>
    /// <param name="logger"></param>
    /// <param name="activityTaskQueue"></param>
    /// <param name="channelServiceClientFactory"></param>
    public class AsyncCloudAdapter(IChannelServiceClientFactory channelServiceClientFactory, ILogger<IBotHttpAdapter> logger, IActivityTaskQueue activityTaskQueue) : CloudAdapter(channelServiceClientFactory, logger)
    {
        private readonly IActivityTaskQueue _activityTaskQueue = activityTaskQueue;

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
        override public async Task ProcessAsync(HttpRequest httpRequest, HttpResponse httpResponse, IBot bot, CancellationToken cancellationToken = default)
        {
            if (httpRequest == null)
            {
                throw new ArgumentNullException(nameof(httpRequest));
            }

            if (httpResponse == null)
            {
                throw new ArgumentNullException(nameof(httpResponse));
            }

            if (bot == null)
            {
                throw new ArgumentNullException(nameof(bot));
            }

            // Get is a socket exchange request, so should be processed by base CloudAdapter
            if (httpRequest.Method == HttpMethods.Get)
            {
                await base.ProcessAsync(httpRequest, httpResponse, bot, cancellationToken);
            }
            else
            {
                // Deserialize the incoming Activity
                var activity = await HttpHelper.ReadRequestAsync<Activity>(httpRequest).ConfigureAwait(false);

                if (string.IsNullOrEmpty(activity?.Type?.ToString()))
                {
                    httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                    Logger.LogWarning("BadRequest: Missing activity or activity type.");
                }
                else if (activity.Type == ActivityTypes.Invoke || activity.DeliveryMode == DeliveryModes.ExpectReplies)
                {
                    // NOTE: Invoke and ExpectReplies cannot be performed async, the response must be written before the calling thread is released.
                    // Process the inbound activity with the bot
                    var invokeResponse = await ProcessActivityAsync(
                        (ClaimsIdentity)httpRequest.HttpContext.User.Identity, 
                        activity, 
                        bot.OnTurnAsync, 
                        cancellationToken).ConfigureAwait(false);

                    // Write the response, potentially serializing the InvokeResponse
                    await HttpHelper.WriteResponseAsync(httpResponse, invokeResponse).ConfigureAwait(false);
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
    }
}
