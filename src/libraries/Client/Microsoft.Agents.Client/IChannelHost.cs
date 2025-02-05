// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using System;

namespace Microsoft.Agents.Client
{
    /// <summary>
    /// Represents a host that contains contains IChannels for Bot-to-bot.
    /// </summary>
    public interface IChannelHost
    {
        string HostAppId { get; }

        /// <summary>
        /// This is the default endpoint to use for the ServiceUrl in Activities sent to an Agent.
        /// This is used when the Channel:ConnectionSettings does not contain the 'ServiceUrl' value.
        /// </summary>
        Uri DefaultHostEndpoint { get; }

        IChannel GetChannel(string alias);
    }
}
