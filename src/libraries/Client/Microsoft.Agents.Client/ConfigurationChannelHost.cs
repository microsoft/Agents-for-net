// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.Agents.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Agents.Client
{
    /// <summary>
    /// Loads bot host information from configuration.
    /// </summary>
    public class ConfigurationChannelHost : IChannelHost
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConnections _connections;

        public ConfigurationChannelHost(IServiceProvider systemServiceProvider, IConnections connections, IConfiguration configuration, string defaultChannelName, string configSection = "ChannelHost")
        {
            ArgumentNullException.ThrowIfNullOrEmpty(configSection);
            _serviceProvider = systemServiceProvider ?? throw new ArgumentNullException(nameof(systemServiceProvider));
            _connections = connections ?? throw new ArgumentNullException(nameof(connections));

            var section = configuration?.GetSection($"{configSection}:Channels");
            var bots = section?.Get<ChannelInfo[]>();
            if (bots != null)
            {
                foreach (var bot in bots)
                {
                    if (string.IsNullOrEmpty(bot.ChannelFactory))
                    {
                        bot.ChannelFactory = defaultChannelName; // Default the channel name to a know name if its not populated in the incomming configuraiton
                    }
                    Channels.Add(bot.Id, bot);
                }
            }

            var hostEndpoint = configuration?.GetValue<string>($"{configSection}:HostEndpoint");
            if (!string.IsNullOrWhiteSpace(hostEndpoint))
            {
                HostEndpoint = new Uri(hostEndpoint);
            }

            var hostAppId = configuration?.GetValue<string>($"{configSection}:HostAppId");
            if (!string.IsNullOrWhiteSpace(hostAppId))
            {
                HostAppId = hostAppId;
            }
        }

        /// <inheritdoc />
        public Uri HostEndpoint { get; }

        /// <inheritdoc />
        public string HostAppId { get; }

        /// <inheritdoc />
        public IDictionary<string, IChannelInfo> Channels { get; } = new Dictionary<string, IChannelInfo>();

        public IChannel GetChannel(string name)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(name);

            if (!Channels.TryGetValue(name, out IChannelInfo channelInfo))
            {
                throw new InvalidOperationException($"IChannelInfo not found for '{name}'");
            }

            return GetChannel(channelInfo);
        }

        public IChannel GetChannel(IChannelInfo channelInfo)
        {
            ArgumentNullException.ThrowIfNull(channelInfo);

            return GetClientFactory(channelInfo).CreateChannel(GetTokenProvider(channelInfo));
        }

        private IChannelFactory GetClientFactory(IChannelInfo channel)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(channel.ChannelFactory);

            return _serviceProvider.GetKeyedService<IChannelFactory>(channel.ChannelFactory) 
                ?? throw new InvalidOperationException($"IBotClientFactory not found for channel '{channel.Id}'");
        }

        private IAccessTokenProvider GetTokenProvider(IChannelInfo channel)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(channel.TokenProvider);

            return _connections.GetConnection(channel.TokenProvider) 
                ?? throw new InvalidOperationException($"IAccessTokenProvider not found for channel '{channel.Id}'");
        }

        private class ChannelInfo : IChannelInfo
        {
            public string Id { get; set; }
            public string AppId { get; set; }
            public string ResourceUrl { get; set; }
            public Uri Endpoint { get; set; }
            public string TokenProvider { get; set; }
            public string ChannelFactory { get; set; }
        }
    }
}
