// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Threading.Channels;
using System.Collections.Concurrent;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.AspNetCore.Http;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    /// <summary>
    /// Internal producer/consumer queue to read Activities sent by the Adapter during DeliveryMode.Stream
    /// </summary>
    internal class StreamedResponseHandler
    {
        private static readonly ConcurrentDictionary<string, Channel<IActivity>> _conversations = new();
        private const string ActivityEventTemplate = "event: activity\r\ndata: {0}\r\n";
        private const string InvokeResponseEventTemplate = "event: invokeResponse\r\ndata: {0}\r\n";

        public static async Task HandleResponsesAsync(string channelConversationId, Action<IActivity> action, CancellationToken cancellationToken)
        {
            // TBD: There is a non-real world race condition if CompleteHandlerForConversation is called before this
            // method.  In which case, this will block forever.

            var channel = _conversations.GetOrAdd(channelConversationId, Channel.CreateUnbounded<IActivity>());

            while (await channel.Reader.WaitToReadAsync(cancellationToken))
            {
                var activity = await channel.Reader.ReadAsync(cancellationToken);
                action(activity);
            }
        }

        public static void CompleteHandlerForConversation(string channelConversationId)
        {
            if (_conversations.TryGetValue(channelConversationId, out var channel))
            {
                channel.Writer.Complete();
                _conversations.Remove(channelConversationId, out _);
            }
        }

        public static async Task SendActivitiesAsync(string conversationId, IActivity[] activities, CancellationToken cancellationToken)
        {
            var channel = _conversations.GetOrAdd(conversationId, Channel.CreateUnbounded<IActivity>());

            foreach (var activity in activities)
            {
                // Write the Activity to the Channel.  It is consumed on the other side via HandleResponses.
                await channel.Writer.WriteAsync(activity, cancellationToken);
            }
        }

        public static async Task StreamActivity(HttpResponse httpResponse, IActivity activity, ILogger logger, CancellationToken cancellationToken)
        {
            try
            {
                await httpResponse.Body.WriteAsync(Encoding.UTF8.GetBytes(string.Format(ActivityEventTemplate, ProtocolJsonSerializer.ToJson(activity))), cancellationToken);
                await httpResponse.Body.FlushAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "StreamActivity");
                throw;
            }

        }

        public static async Task StreamInvokeResponse(HttpResponse httpResponse, InvokeResponse invokeResponse, ILogger logger, CancellationToken cancellationToken)
        {
            try
            {
                if (invokeResponse?.Body != null)
                {
                    await httpResponse.Body.WriteAsync(Encoding.UTF8.GetBytes(string.Format(InvokeResponseEventTemplate, ProtocolJsonSerializer.ToJson(invokeResponse))), cancellationToken);
                    await httpResponse.Body.FlushAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "StreamInvokeResponse");
                throw;
            }

        }
    }
}
