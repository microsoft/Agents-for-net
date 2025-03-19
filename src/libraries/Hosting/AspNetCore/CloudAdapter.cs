﻿// Copyright (c) Microsoft Corporation. All rights reserved.
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
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Serialization;

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
        : ChannelServiceAdapterBase, IBotHttpAdapter
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
        /// <exception cref="ArgumentNullException"></exception>
        public CloudAdapter(
            IChannelServiceClientFactory channelServiceClientFactory,
            IActivityTaskQueue activityTaskQueue,
            ILogger<IBotHttpAdapter> logger = null,
            AdapterOptions options = null,
            BotBuilder.IMiddleware[] middlewares = null) : base(channelServiceClientFactory, logger)
        {
            _activityTaskQueue = activityTaskQueue ?? throw new ArgumentNullException(nameof(activityTaskQueue));
            _adapterOptions = options ?? new AdapterOptions() { Async = true, ShutdownTimeoutSeconds = 60 };

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
                exception.GetExceptionDetail(sbError,iLevel); // ExceptionParser
                if (exception is ErrorResponseException errorResponse && errorResponse.Body != null)
                {
                    sbError.Append(Environment.NewLine);
                    sbError.Append(errorResponse.Body.ToString());
                }
                string resolvedErrorMessage = sbError.ToString();
                
                // Writing formatted exception message to log with error codes and help links. 
                logger.LogError(resolvedErrorMessage);

                if (exception is not OperationCanceledException) // Do not try to send another message if the response has been canceled.
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text(resolvedErrorMessage), CancellationToken.None);
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

            if (httpRequest.Method != HttpMethods.Post)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            }
            else
            {
                // Deserialize the incoming Activity
                var activity = await HttpHelper.ReadRequestAsync<IActivity>(httpRequest).ConfigureAwait(false);
                var claimsIdentity = (ClaimsIdentity)httpRequest.HttpContext.User.Identity;

                if (!IsValidChannelActivity(activity, httpResponse))
                {
                    httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }

                try
                {
                    if (activity.DeliveryMode == DeliveryModes.Stream)
                    {
                        InvokeResponse invokeResponse = null;

                        // Queue the activity to be processed by the ActivityBackgroundService, and stop SynchronousRequestHandler when the
                        // turn is done.
                        _activityTaskQueue.QueueBackgroundActivity(claimsIdentity, activity, onComplete: (response) =>
                        {
                            SynchronousRequestHandler.CompleteHandlerForConversation(activity.Conversation.Id);
                            invokeResponse = response;
                        });

                        // block until turn is complete
                        await SynchronousRequestHandler.HandleResponsesAsync(activity.Conversation.Id, async (activity) =>
                        {
                            try
                            {
                                await httpResponse.Body.WriteAsync(Encoding.UTF8.GetBytes($"event: activity\r\ndata: {ProtocolJsonSerializer.ToJson(activity)}\r\n"), cancellationToken);
                                await httpResponse.Body.FlushAsync(cancellationToken);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.ToString());
                            }
                        }, cancellationToken).ConfigureAwait(false);

                        if (invokeResponse?.Body != null)
                        {
                            await httpResponse.Body.WriteAsync(Encoding.UTF8.GetBytes($"event: invokeResponse\r\ndata: {ProtocolJsonSerializer.ToJson(invokeResponse)}\r\n"), cancellationToken);
                            await httpResponse.Body.FlushAsync(cancellationToken);
                        }
                    }
                    else if (!_adapterOptions.Async || activity.Type == ActivityTypes.Invoke || activity.DeliveryMode == DeliveryModes.ExpectReplies)
                    {
                        // Invoke and ExpectReplies cannot be performed async, the response must be written before the calling thread is released.
                        // Process the inbound activity with the bot
                        var invokeResponse = await ProcessActivityAsync(claimsIdentity, activity, bot.OnTurnAsync, cancellationToken).ConfigureAwait(false);

                        // Write the response, potentially serializing the InvokeResponse
                        await HttpHelper.WriteResponseAsync(httpResponse, invokeResponse).ConfigureAwait(false);
                    }
                    else
                    {
                        // Queue the activity to be processed by the ActivityBackgroundService
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
        /// CloudAdapter handles this override asynchronously.
        /// </summary>
        /// <param name="claimsIdentity"></param>
        /// <param name="continuationActivity"></param>
        /// <param name="bot"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="audience"></param>
        /// <returns></returns>
        public override Task ProcessProactiveAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, IBot bot, CancellationToken cancellationToken, string audience = null)
        {
            if (_adapterOptions.Async)
            {
                // Queue the activity to be processed by the ActivityBackgroundService
                _activityTaskQueue.QueueBackgroundActivity(claimsIdentity, continuationActivity, proactive: true, proactiveAudience: audience);
                return Task.CompletedTask;
            }

            return base.ProcessProactiveAsync(claimsIdentity, continuationActivity, bot, cancellationToken, audience);
        }

        protected override async Task<bool> StreamedResponseAsync(IActivity incomingActivity, IActivity outActivity, CancellationToken cancellationToken)
        {
            if (incomingActivity.DeliveryMode != DeliveryModes.Stream)
            {
                return false;
            }

            await SynchronousRequestHandler.SendActivitiesAsync(incomingActivity.Conversation.Id, [outActivity], cancellationToken).ConfigureAwait(false);

            return true;
        }

        private bool IsValidChannelActivity(IActivity activity, HttpResponse httpResponse)
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
