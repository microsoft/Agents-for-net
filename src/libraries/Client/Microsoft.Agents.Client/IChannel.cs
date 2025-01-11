// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Client
{
    public interface IChannel : IDisposable
    {
        /// <summary>
        /// Sends an activity to a channel.
        /// </summary>
        /// <param name="toBotId">The AppId of the bot receiving the activity.</param>
        /// <param name="endpoint">The URL of the bot receiving the activity.</param>
        /// <param name="serviceUrl">The ServiceUrl of the channel host.</param>
        /// <param name="conversationId">A conversation ID to use for the conversation with the channel.</param>
        /// <param name="activity">The <see cref="IActivity"/> to send to forward.</param>
        /// <param name="cancellationToken">cancellation Token.</param>
        /// <param name="toBotResource"></param>
        /// <returns>Async task with optional invokeResponse.</returns>
        Task<InvokeResponse> PostActivityAsync(string toBotId, string toBotResource, Uri endpoint, Uri serviceUrl, string conversationId, IActivity activity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends an activity to a channel.
        /// </summary>
        /// <typeparam name="T">The type of body in the InvokeResponse.</typeparam>
        /// <param name="toBotId">The AppId of the bot receiving the activity.</param>
        /// <param name="endpoint">The URL of the bot receiving the activity.</param>
        /// <param name="serviceUrl">The ServiceUrl of the channel host.</param>
        /// <param name="conversationId">A conversation ID to use for the conversation with the channel.</param>
        /// <param name="activity">The <see cref="IActivity"/> to send to forward.</param>
        /// <param name="cancellationToken">cancellation Token.</param>
        /// <param name="toBotResource"></param>
        /// <returns>Async task with optional typed InvokeResponse.</returns>
        Task<InvokeResponse<T>> PostActivityAsync<T>(string toBotId, string toBotResource, Uri endpoint, Uri serviceUrl, string conversationId, IActivity activity, CancellationToken cancellationToken = default);
    }
}
