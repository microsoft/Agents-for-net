﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using Xunit;

namespace Microsoft.Agents.Client.Tests
{
    public class ConfigurationChannelHostTests
    {
        private readonly string _defaultChannel = "webchat";
        private readonly Mock<IKeyedServiceProvider> _provider = new();
        private readonly Mock<IConnections> _connections = new();
        private readonly IConfigurationRoot _config = new ConfigurationBuilder().Build();
        private readonly Mock<HttpBotChannelSettings> _channelInfo = new();
        private readonly Mock<IAccessTokenProvider> _token = new();
        private readonly Mock<IChannel> _channel = new();
        private readonly Mock<IConversationIdFactory> _conversationIdFactory = new();
        private readonly Mock<IHttpClientFactory> _httpClientFactory = new();

        [Fact]
        public void Constructor_ShouldThrowOnNullConfigSection()
        {
            Assert.Throws<ArgumentNullException>(() => new ConfigurationChannelHost(_config, _provider.Object, _conversationIdFactory.Object, _connections.Object, _httpClientFactory.Object));
        }

        [Fact]
        public void Constructor_ShouldThrowOnEmptyConfigSection()
        {
            Assert.Throws<ArgumentException>(() => new ConfigurationChannelHost(_config, _provider.Object, _conversationIdFactory.Object, _connections.Object, _httpClientFactory.Object));
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullServiceProvider()
        {
            Assert.Throws<ArgumentNullException>(() => new ConfigurationChannelHost(_config, null, _conversationIdFactory.Object, _connections.Object, _httpClientFactory.Object));
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullConnections()
        {
            Assert.Throws<ArgumentNullException>(() => new ConfigurationChannelHost(_config, _provider.Object, _conversationIdFactory.Object, _connections.Object, _httpClientFactory.Object));
        }

        [Fact]
        public void Constructor_ShouldSetProperties()
        {
            var botId = "bot1";
            var appId = "123";
            var endpoint = "http://localhost/";
            var sections = new Dictionary<string, string>{
                {"ChannelHost:Channels:0:Alias", botId},
                {"ChannelHost:DefaultHostEndpoint", endpoint},
                {"ChannelHost:HostAppId", appId},
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(sections)
                .Build();

            var host = new ConfigurationChannelHost(_config, _provider.Object, _conversationIdFactory.Object, _connections.Object, _httpClientFactory.Object);

            Assert.Single(host.Channels);
            Assert.Equal(botId, host.Channels[botId].Alias);
            Assert.Equal(endpoint, host.DefaultHostEndpoint.ToString());
            Assert.Equal(appId, host.HostClientId);
        }

        [Fact]
        public void GetChannel_ShouldThrowOnNullName()
        {
            var host = new ConfigurationChannelHost(_config, _provider.Object, _conversationIdFactory.Object, _connections.Object, _httpClientFactory.Object);

            Assert.Throws<ArgumentException>(() => host.GetChannel(string.Empty ?? null));
        }

        [Fact]
        public void GetChannel_ShouldThrowOnEmptyName()
        {
            var host = new ConfigurationChannelHost(_config, _provider.Object, _conversationIdFactory.Object, _connections.Object, _httpClientFactory.Object);

            Assert.Throws<ArgumentException>(() => host.GetChannel(string.Empty));
        }

        [Fact]
        public void GetChannel_ShouldThrowOnUnknownChannel()
        {
            var host = new ConfigurationChannelHost(_config, _provider.Object, _conversationIdFactory.Object, _connections.Object, _httpClientFactory.Object);

            Assert.Throws<InvalidOperationException>(() => host.GetChannel("random"));
        }

        [Fact]
        public void GetChannel_ShouldThrowOnNullChannel()
        {
            var host = new ConfigurationChannelHost(_config, _provider.Object, _conversationIdFactory.Object, _connections.Object, _httpClientFactory.Object);
            host.Channels.Add(_defaultChannel, null);

            Assert.Throws<ArgumentNullException>(() => host.GetChannel(_defaultChannel));
        }

        [Fact]
        public void GetChannel_ShouldThrowOnNullChannelTokenProvider()
        {
            _channelInfo.SetupGet(e => e.ConnectionSettings.TokenProvider)
                .Returns(() => null)
                .Verifiable(Times.Once);

            var host = new ConfigurationChannelHost(_config, _provider.Object, _conversationIdFactory.Object, _connections.Object, _httpClientFactory.Object);
            host.Channels.Add(_defaultChannel, _channelInfo.Object);

            Assert.Throws<ArgumentNullException>(() => host.GetChannel(_defaultChannel));
            Mock.Verify(_channelInfo, _provider);
        }

        [Fact]
        public void GetChannel_ShouldThrowOnEmptyChannelTokenProvider()
        {
            _channelInfo.SetupGet(e => e.ConnectionSettings.TokenProvider)
                .Returns(string.Empty)
                .Verifiable(Times.Once);

            var host = new ConfigurationChannelHost(_config, _provider.Object, _conversationIdFactory.Object, _connections.Object, _httpClientFactory.Object);
            host.Channels.Add(_defaultChannel, _channelInfo.Object);

            Assert.Throws<ArgumentException>(() => host.GetChannel(_defaultChannel));
            Mock.Verify(_channelInfo, _provider);
        }

        [Fact]
        public void GetChannel_ShouldThrowOnNullConnection()
        {
            _channelInfo.SetupGet(e => e.ConnectionSettings.TokenProvider)
                .Returns("provider")
                .Verifiable(Times.Exactly(2));
            _connections.Setup(e => e.GetConnection(It.IsAny<string>()))
                .Returns<IAccessTokenProvider>(null)
                .Verifiable(Times.Once);

            var host = new ConfigurationChannelHost(_config, _provider.Object, _conversationIdFactory.Object, _connections.Object, _httpClientFactory.Object);
            host.Channels.Add(_defaultChannel, _channelInfo.Object);

            Assert.Throws<InvalidOperationException>(() => host.GetChannel(_defaultChannel));
            Mock.Verify(_channelInfo, _provider, _connections);
        }

        [Fact]
        public void GetChannel_ShouldReturnChannel()
        {
            _channelInfo.SetupGet(e => e.ConnectionSettings.TokenProvider)
                .Returns("provider")
                .Verifiable(Times.Exactly(2));
            _connections.Setup(e => e.GetConnection(It.IsAny<string>()))
                .Returns(_token.Object)
                .Verifiable(Times.Once);

            var host = new ConfigurationChannelHost(_config, _provider.Object, _conversationIdFactory.Object, _connections.Object, _httpClientFactory.Object);
            host.Channels.Add(_defaultChannel, _channelInfo.Object);
            var result = host.GetChannel(_defaultChannel);

            Assert.Equal(_channel.Object, result);
            Mock.Verify(_channelInfo, _provider, _connections);
        }
    }
}
