// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Threading.Channels;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Core.Models.Activities;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    /// <summary>
    /// Producer/consumer queue to read Activities sent by the Adapter during asynchronous background requests such as
    /// DeliveryMode.Stream/ExpectReplies.
    /// </summary>
    /// <remarks>
    /// StartHandlerForRequest and HandleResponsesAsync are called from the request thread.  SendActivitiesAsync would
    /// ultimately be called from the background thread as Activities are sent through Adapter. CompleteHandlerForRequest
    /// is used to signal the queue for the request is complete and not further Activities will be queued.
    /// </remarks>
    public class ChannelResponseQueue(ILogger logger)
    {
        private readonly ConcurrentDictionary<string, ChannelInfo> _conversations = new();

        /// <summary>
        /// Processes queued responses.  This blocks until CompleteHandlerForRequest is called.
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="action">Action to call when an Activity is received.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task HandleResponsesAsync(string requestId, Action<IActivity> action, CancellationToken cancellationToken)
        {
            if (_conversations.TryGetValue(requestId, out var channelInfo))
            {

                while (await channelInfo.channel.Reader.WaitToReadAsync(cancellationToken))
                {
                    var activity = await channelInfo.channel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                    action(activity);
                }

                channelInfo.readDone.Set();
            }
        }

        public void ReadAllResponsesAsync(string requestId, Action<IActivity> action)
        {
            if (_conversations.TryGetValue(requestId, out var channelInfo))
            {

                while (channelInfo.channel.Reader.TryRead(out var activity))
                {
                    action(activity);
                }

                channelInfo.readDone.Set();
            }
        }

        /// <summary>
        /// Starts the queue for a request.  This MUST be called before background processing starts.
        /// </summary>
        /// <param name="requestId"></param>
        public void StartHandlerForRequest(string requestId)
        {
            _conversations.GetOrAdd(requestId, new ChannelInfo());
        }

        /// <summary>
        /// Completes channel response handling.  This will wait for all reads to complete.  Once called,
        /// any subsequent SendActivitiesAsync are ignored.
        /// </summary>
        /// <param name="requestId"></param>
        public void CompleteHandlerForRequest(string requestId)
        {
            if (_conversations.TryGetValue(requestId, out var channelInfo))
            {
                channelInfo.channel.Writer.Complete();
                _conversations.Remove(requestId, out _);
            }

            // wait for reads to be done
            channelInfo.readDone.WaitOne();
        }

        /// <summary>
        /// Called by the background processing to queue Activities.  This will signal HandleResponsesAsync to
        /// process the Activities.
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="activities"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task SendActivitiesAsync(string requestId, IActivity[] activities, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(requestId) || !_conversations.TryGetValue(requestId, out var channelInfo))
            {
                logger.LogWarning("ChannelResponseQueue received unknown requestId '{RequestId}' for Activities: {Activity}", requestId, ProtocolJsonSerializer.ToJson(activities));
            }
            else
            {
                foreach (var activity in activities)
                {
                    // Write the Activity to the Channel.  It is consumed on the other side via HandleResponses.
                    await channelInfo.channel.Writer.WriteAsync(activity, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    struct ChannelInfo
    {
        public ChannelInfo()
        {
        }

        public EventWaitHandle readDone = new(false, EventResetMode.ManualReset);
        public Channel<IActivity> channel = Channel.CreateUnbounded<IActivity>();
    }
}
