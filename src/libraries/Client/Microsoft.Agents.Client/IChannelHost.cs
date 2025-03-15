// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Client
{
    /// <summary>
    /// Represents a host the contains IChannels for Bot-to-bot.
    /// </summary>
    public interface IChannelHost
    {
        /// <summary>
        /// The endpoint to use in Activity.ServiceUrl.
        /// </summary>
        Uri HostEndpoint { get; }

        string HostAppId { get; }

        /// <summary>
        /// The bots the host knows about.
        /// </summary>
        IDictionary<string, IChannelInfo> Channels { get; }

        IChannel GetChannel(IChannelInfo channelInfo);

        IChannel GetChannel(string name);

        Task<string> CreateConversationId(string channelName, ClaimsIdentity identity, IActivity activity, CancellationToken cancellationToken = default);
        Task<BotConversationReference> GetBotConversationReferenceAsync(string channelConversationId, CancellationToken cancellationToken);
        Task DeleteConversationReferenceAsync(string channelConversationId, CancellationToken cancellationToken);

        Task SendToChannel(string channelConversationId, string channelName, IActivity activity, CancellationToken cancellationToken = default);
    }
}
