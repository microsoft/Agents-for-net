// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Threading.Channels;
using System.Collections.Concurrent;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    /// <summary>
    /// Internal producer/consumer queue to read Activities sent by the Adapter during DeliveryMode.Stream
    /// </summary>
    internal class StreamedResponseHandler
    {
        private static readonly ConcurrentDictionary<string, Channel<IActivity>> _conversations = new();
        public static readonly IStreamedResponseWriter DefaultWriter = new ActivityStreamedResponseWriter();

        public static async Task HandleResponsesAsync(string channelConversationId, Action<IActivity> action, CancellationToken cancellationToken)
        {
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
    }
}
