// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using System;

namespace Microsoft.Agents.Client
{
    /// <summary>
    /// Represents a host the contains IChannels for Bot-to-bot.
    /// </summary>
    public interface IChannelHost
    {
        string HostAppId { get; }
        Uri DefaultHostEndpoint { get; }

        IChannel GetChannel(string alias);
    }
}
