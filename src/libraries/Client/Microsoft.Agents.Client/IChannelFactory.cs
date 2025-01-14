// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Client
{
    public interface IChannelFactory
    {
        /// <summary>
        /// Creates a <see cref="IChannel"/> used for calling another bot.
        /// </summary>
        /// <returns>A <see cref="IChannel"/> instance to call bots.</returns>
        IChannel CreateChannel(IChannelHost host, IChannelInfo channelInfo);
    }
}
