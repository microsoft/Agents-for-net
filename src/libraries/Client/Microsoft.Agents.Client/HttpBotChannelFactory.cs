// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Net.Http;

namespace Microsoft.Agents.Client
{
    /// <summary>
    /// An HTTP based IChannelFactory factory.
    /// </summary>
    /// <param name="connections"></param>
    /// <param name="httpClientFactory"></param>
    /// <param name="synchronousChannelResponseHandler"></param>
    /// <param name="logger"></param>
    public class HttpBotChannelFactory(
        IConnections connections, 
        IHttpClientFactory httpClientFactory, 
        ILogger < HttpBotChannelFactory> logger = null) : IChannelFactory
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        private readonly ILogger<HttpBotChannelFactory> _logger = logger ?? NullLogger<HttpBotChannelFactory>.Instance;
        private readonly IConnections _connections = connections ?? throw new ArgumentNullException(nameof(connections));

        /// <inheritdoc />
        public IChannel CreateChannel(IChannelInfo channelInfo)
        {
            ValidateChannel(channelInfo);

            var httpClient = _httpClientFactory.CreateClient(channelInfo.ConnectionSettings?["NamedClient"]);

            var tokenProviderName = channelInfo.ConnectionSettings?["TokenProvider"];
            var tokenProvider = _connections.GetConnection(tokenProviderName) ?? throw new ArgumentException($"TokenProvider {tokenProviderName} not found for Channel {channelInfo.Alias}");

            return new HttpBotChannel(channelInfo, httpClient, tokenProvider);
        }

        private static void ValidateChannel(IChannelInfo channel)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(channel.Alias);
            ArgumentException.ThrowIfNullOrWhiteSpace(channel.ChannelFactory);
        }
    }
}
