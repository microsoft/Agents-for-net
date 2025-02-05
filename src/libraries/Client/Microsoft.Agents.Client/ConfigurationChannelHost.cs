// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
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

        public ConfigurationChannelHost(IServiceProvider systemServiceProvider, IConfiguration configuration, string defaultChannelFactory, string configSection = "ChannelHost")
        {
            ArgumentException.ThrowIfNullOrEmpty(configSection);
            _serviceProvider = systemServiceProvider ?? throw new ArgumentNullException(nameof(systemServiceProvider));

            var section = configuration?.GetSection($"{configSection}:Channels");
            var channels = section?.Get<ChannelInfo[]>();
            if (channels != null)
            {
                foreach (var channel in channels)
                {
                    if (string.IsNullOrEmpty(channel.ChannelFactory))
                    {
                        channel.ChannelFactory = defaultChannelFactory;
                    }

                    ValidateChannel(channel);
                    Channels.Add(channel.Alias, channel);
                }
            }

            var hostEndpoint = configuration?.GetValue<string>($"{configSection}:DefaultHostEndpoint");
            if (!string.IsNullOrWhiteSpace(hostEndpoint))
            {
                DefaultHostEndpoint = new Uri(hostEndpoint);
            }

            var hostAppId = configuration?.GetValue<string>($"{configSection}:HostClientId");
            if (!string.IsNullOrWhiteSpace(hostAppId))
            {
                HostAppId = hostAppId;
            }
        }

        /// <inheritdoc />
        public Uri DefaultHostEndpoint { get; }

        /// <inheritdoc />
        public string HostAppId { get; }

        internal IDictionary<string, IChannelInfo> Channels { get; } = new Dictionary<string, IChannelInfo>();

        /// <inheritdoc/>
        public IChannel GetChannel(string alias)
        {
            ArgumentException.ThrowIfNullOrEmpty(alias);

            if (!Channels.TryGetValue(alias, out IChannelInfo channelInfo))
            {
                throw new InvalidOperationException($"IChannelInfo not found for '{alias}'");
            }

            return GetChannel(channelInfo);
        }

        private IChannel GetChannel(IChannelInfo channelInfo)
        {
            ArgumentNullException.ThrowIfNull(channelInfo);

            return GetClientFactory(channelInfo).CreateChannel(this, channelInfo);
        }

        private IChannelFactory GetClientFactory(IChannelInfo channel)
        {
            ArgumentException.ThrowIfNullOrEmpty(channel.ChannelFactory);

            return _serviceProvider.GetKeyedService<IChannelFactory>(channel.ChannelFactory) 
                ?? throw new InvalidOperationException($"IChannelFactory not found for channel '{channel.Alias}'");
        }

        private class ChannelInfo : IChannelInfo
        {
            public string Alias { get; set; }
            public string DisplayName { get; set; }
            public string ChannelFactory { get; set; }
            public IDictionary<string, string> ConnectionSettings { get; set; }
        }

        private static void ValidateChannel(IChannelInfo channel)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(channel.Alias);
            ArgumentException.ThrowIfNullOrWhiteSpace(channel.ChannelFactory);
        }
    }
}
