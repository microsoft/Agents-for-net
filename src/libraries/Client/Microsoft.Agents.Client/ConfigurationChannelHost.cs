// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core.Models;
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
        private readonly IConversationIdFactory _conversationIdFactory;

        public ConfigurationChannelHost(IServiceProvider systemServiceProvider, IConversationIdFactory conversationIdFactory, IConnections connections, IConfiguration configuration, string defaultChannelName, string configSection = "ChannelHost")
        {
            ArgumentException.ThrowIfNullOrEmpty(configSection);
            _serviceProvider = systemServiceProvider ?? throw new ArgumentNullException(nameof(systemServiceProvider));
            _connections = connections ?? throw new ArgumentNullException(nameof(connections));
            _conversationIdFactory = conversationIdFactory ?? throw new ArgumentNullException(nameof(conversationIdFactory));

            var section = configuration?.GetSection($"{configSection}:Channels");
            var bots = section?.Get<ChannelInfo[]>();
            if (bots != null)
            {
                foreach (var bot in bots)
                {
                    if (string.IsNullOrEmpty(bot.ChannelFactory))
                    {
                        bot.ChannelFactory = defaultChannelName; // Default the channel name to a know name if its not populated in the incoming configuration
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

        public async Task<string> CreateConversationId(string channelName, ClaimsIdentity identity, IActivity activity, CancellationToken cancellationToken = default)
        {
            var options = new ConversationIdFactoryOptions
            {
                FromBotOAuthScope = BotClaims.GetTokenScopes(identity)?.First(),
                FromBotId = HostAppId,
                Activity = activity,
                Bot = Channels[channelName]
            };
            return await _conversationIdFactory.CreateConversationIdAsync(options, cancellationToken);
        }

        public async Task SendToChannel(string channelConversationId, string channelName, IActivity activity, CancellationToken cancellationToken = default)
        {
            var targetChannelInfo = Channels[channelName];
            using var channel = GetChannel(channelName);

            // route the activity to the skill
            var response = await channel.PostActivityAsync(
                targetChannelInfo.AppId, 
                targetChannelInfo.ResourceUrl,
                targetChannelInfo.Endpoint, 
                HostEndpoint,
                channelConversationId, 
                activity, 
                cancellationToken);

            // Check response status
            if (!(response.Status >= 200 && response.Status <= 299))
            {
                throw new HttpRequestException($"Error invoking the bot id: \"{targetChannelInfo.Id}\" at \"{targetChannelInfo.Endpoint}\" (status is {response.Status}). \r\n {response.Body}");
            }
        }

        public Task<BotConversationReference> GetBotConversationReferenceAsync(string channelConversationId, CancellationToken cancellationToken)
        {
            return _conversationIdFactory.GetBotConversationReferenceAsync(channelConversationId, cancellationToken);
        }

        public Task DeleteConversationReferenceAsync(string channelConversationId, CancellationToken cancellationToken)
        {
            return _conversationIdFactory.DeleteConversationReferenceAsync(channelConversationId, cancellationToken);
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
