// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Client
{
    internal class HttpBotChannelSettings
    {
        public HttpBotChannelSettings(IChannelInfo channelInfo)
        {
            ArgumentNullException.ThrowIfNull(channelInfo);

            ClientId = channelInfo.ConnectionSettings[nameof(ClientId)];
            Endpoint = new(channelInfo.ConnectionSettings[nameof(Endpoint)]);
            TokenProvider = channelInfo.ConnectionSettings[nameof(TokenProvider)];
            ServiceUrl = channelInfo.ConnectionSettings[nameof(ServiceUrl)];

            if (channelInfo.ConnectionSettings.TryGetValue(nameof(ResourceUrl), out var resourceUrl))
            {
                ResourceUrl = resourceUrl;
            }

            ValidateSettings();
        }

        /// <summary>
        /// Gets or sets clientId/appId of the channel.
        /// </summary>
        /// <value>
        /// ClientId/AppId of the channel.
        /// </value>
        public string ClientId { get; set; }

        public string ResourceUrl { get; set; }

        /// <summary>
        /// Gets or sets provider name for tokens.
        /// </summary>
        public string TokenProvider { get; set; }

        /// <summary>
        /// Gets or sets endpoint for the channel.
        /// </summary>
        /// <value>
        /// Uri for the channel.
        /// </value>
        public Uri Endpoint { get; set; }

        public string ServiceUrl { get; set; }

        private void ValidateSettings()
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(ClientId);
            ArgumentNullException.ThrowIfNull(Endpoint);
            ArgumentException.ThrowIfNullOrWhiteSpace(TokenProvider);
            ArgumentException.ThrowIfNullOrWhiteSpace(ServiceUrl);

            if (string.IsNullOrEmpty(ResourceUrl))
            {
                ResourceUrl = $"api://{ClientId}";
            }
        }
    }
}
