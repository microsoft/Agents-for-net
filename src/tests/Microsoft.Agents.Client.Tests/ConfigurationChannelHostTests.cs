// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Agents.Client.Tests
{
    public class ConfigurationChannelHostTests
    {
        private readonly string _defaultChannel = "webchat";
        private readonly Mock<IKeyedServiceProvider> _provider = new();
        private readonly Mock<IConnections> _connections = new();
        private readonly IConfigurationRoot _config = new ConfigurationBuilder().Build();
        private readonly Mock<IChannelInfo> _channelInfo = new();
        private readonly Mock<IChannelFactory> _channelFactory = new();
        private readonly Mock<IAccessTokenProvider> _token = new();
        private readonly Mock<IChannel> _channel = new();
        private readonly Mock<IConversationIdFactory> _conversationIdFactory = new();

        [Fact]
        public void Constructor_ShouldThrowOnNullConfigSection()
        {
            Assert.Throws<ArgumentNullException>(() => new ConfigurationChannelHost(_provider.Object, _conversationIdFactory.Object, _connections.Object, _config, _defaultChannel, null));
        }

        [Fact]
        public void Constructor_ShouldThrowOnEmptyConfigSection()
        {
            Assert.Throws<ArgumentException>(() => new ConfigurationChannelHost(_provider.Object, _conversationIdFactory.Object, _connections.Object, _config, _defaultChannel, string.Empty));
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullServiceProvider()
        {
            Assert.Throws<ArgumentNullException>(() => new ConfigurationChannelHost(null, _conversationIdFactory.Object, _connections.Object, _config, _defaultChannel));
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullConnections()
        {
            Assert.Throws<ArgumentNullException>(() => new ConfigurationChannelHost(_provider.Object, _conversationIdFactory.Object, null, _config, _defaultChannel));
        }

        [Fact]
        public void Constructor_ShouldSetProperties()
        {
            var botId = "bot1";
            var appId = "123";
            var channel = "testing";
            var endpoint = "http://localhost/";
            var sections = new Dictionary<string, string>{
                {"ChannelHost:Channels:0:Id", botId},
                {"ChannelHost:HostEndpoint", endpoint},
                {"ChannelHost:HostAppId", appId},
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(sections)
                .Build();

            var host = new ConfigurationChannelHost(_provider.Object, _conversationIdFactory.Object, _connections.Object, config, channel);

            Assert.Single(host.Channels);
            Assert.Equal(botId, host.Channels[botId].Id);
            Assert.Equal(channel, host.Channels[botId].ChannelFactory);
            Assert.Equal(endpoint, host.HostEndpoint.ToString());
            Assert.Equal(appId, host.HostAppId);
        }

        [Fact]
        public void GetChannel_ShouldThrowOnNullName()
        {
            var host = new ConfigurationChannelHost(_provider.Object, _conversationIdFactory.Object, _connections.Object, _config, _defaultChannel);

            Assert.Throws<ArgumentException>(() => host.GetChannel(string.Empty ?? null));
        }

        [Fact]
        public void GetChannel_ShouldThrowOnEmptyName()
        {
            var host = new ConfigurationChannelHost(_provider.Object, _conversationIdFactory.Object, _connections.Object, _config, _defaultChannel);

            Assert.Throws<ArgumentException>(() => host.GetChannel(string.Empty));
        }

        [Fact]
        public void GetChannel_ShouldThrowOnUnknownChannel()
        {
            var host = new ConfigurationChannelHost(_provider.Object, _conversationIdFactory.Object, _connections.Object, _config, _defaultChannel);

            Assert.Throws<InvalidOperationException>(() => host.GetChannel("random"));
        }

        [Fact]
        public void GetChannel_ShouldThrowOnNullChannel()
        {
            var host = new ConfigurationChannelHost(_provider.Object, _conversationIdFactory.Object, _connections.Object, _config, _defaultChannel);
            host.Channels.Add(_defaultChannel, null);

            Assert.Throws<ArgumentNullException>(() => host.GetChannel(_defaultChannel));
        }

        [Fact]
        public void GetChannel_ShouldThrowOnNullChannelFactory()
        {
            _channelInfo.SetupGet(e => e.ChannelFactory)
                .Returns(() => null)
                .Verifiable(Times.Once);

            var host = new ConfigurationChannelHost(_provider.Object, _conversationIdFactory.Object, _connections.Object, _config, _defaultChannel);
            host.Channels.Add(_defaultChannel, _channelInfo.Object);

            Assert.Throws<ArgumentNullException>(() => host.GetChannel(_defaultChannel));
            Mock.Verify(_channelInfo);
        }

        [Fact]
        public void GetChannel_ShouldThrowOnEmptyChannelFactory()
        {
            _channelInfo.SetupGet(e => e.ChannelFactory)
                .Returns(string.Empty)
                .Verifiable(Times.Once);

            var host = new ConfigurationChannelHost(_provider.Object, _conversationIdFactory.Object, _connections.Object, _config, _defaultChannel);
            host.Channels.Add(_defaultChannel, _channelInfo.Object);

            Assert.Throws<ArgumentException>(() => host.GetChannel(_defaultChannel));
            Mock.Verify(_channelInfo);
        }

        [Fact]
        public void GetChannel_ShouldThrowOnNullKeyedService()
        {
            _channelInfo.SetupGet(e => e.ChannelFactory)
                .Returns("factory")
                .Verifiable(Times.Exactly(2));
            _provider.Setup(e => e.GetKeyedService(It.IsAny<Type>(), It.IsAny<string>()))
                .Returns<object>(null)
                .Verifiable(Times.Once);

            var host = new ConfigurationChannelHost(_provider.Object, _conversationIdFactory.Object, _connections.Object, _config, _defaultChannel);
            host.Channels.Add(_defaultChannel, _channelInfo.Object);

            Assert.Throws<InvalidOperationException>(() => host.GetChannel(_defaultChannel));
            Mock.Verify(_provider);
        }

        [Fact]
        public void GetChannel_ShouldThrowOnNullChannelTokenProvider()
        {
            _channelInfo.SetupGet(e => e.ChannelFactory)
                .Returns("factory")
                .Verifiable(Times.Exactly(2));
            _channelInfo.SetupGet(e => e.TokenProvider)
                .Returns(() => null)
                .Verifiable(Times.Once);
            _provider.Setup(e => e.GetKeyedService(It.IsAny<Type>(), It.IsAny<string>()))
                .Returns(_channelFactory.Object)
                .Verifiable(Times.Once);

            var host = new ConfigurationChannelHost(_provider.Object, _conversationIdFactory.Object, _connections.Object, _config, _defaultChannel);
            host.Channels.Add(_defaultChannel, _channelInfo.Object);

            Assert.Throws<ArgumentNullException>(() => host.GetChannel(_defaultChannel));
            Mock.Verify(_channelInfo, _provider);
        }

        [Fact]
        public void GetChannel_ShouldThrowOnEmptyChannelTokenProvider()
        {
            _channelInfo.SetupGet(e => e.ChannelFactory)
                .Returns("factory")
                .Verifiable(Times.Exactly(2));
            _channelInfo.SetupGet(e => e.TokenProvider)
                .Returns(string.Empty)
                .Verifiable(Times.Once);
            _provider.Setup(e => e.GetKeyedService(It.IsAny<Type>(), It.IsAny<string>()))
                .Returns(_channelFactory.Object)
                .Verifiable(Times.Once);

            var host = new ConfigurationChannelHost(_provider.Object, _conversationIdFactory.Object, _connections.Object, _config, _defaultChannel);
            host.Channels.Add(_defaultChannel, _channelInfo.Object);

            Assert.Throws<ArgumentException>(() => host.GetChannel(_defaultChannel));
            Mock.Verify(_channelInfo, _provider);
        }

        [Fact]
        public void GetChannel_ShouldThrowOnNullConnection()
        {
            _channelInfo.SetupGet(e => e.ChannelFactory)
                .Returns("factory")
                .Verifiable(Times.Exactly(2));
            _channelInfo.SetupGet(e => e.TokenProvider)
                .Returns("provider")
                .Verifiable(Times.Exactly(2));
            _provider.Setup(e => e.GetKeyedService(It.IsAny<Type>(), It.IsAny<string>()))
                .Returns(_channelFactory.Object)
                .Verifiable(Times.Once);
            _connections.Setup(e => e.GetConnection(It.IsAny<string>()))
                .Returns<IAccessTokenProvider>(null)
                .Verifiable(Times.Once);

            var host = new ConfigurationChannelHost(_provider.Object, _conversationIdFactory.Object, _connections.Object, _config, _defaultChannel);
            host.Channels.Add(_defaultChannel, _channelInfo.Object);

            Assert.Throws<InvalidOperationException>(() => host.GetChannel(_defaultChannel));
            Mock.Verify(_channelInfo, _provider, _connections);
        }

        [Fact]
        public void GetChannel_ShouldReturnChannel()
        {
            _channelInfo.SetupGet(e => e.ChannelFactory)
                .Returns("factory")
                .Verifiable(Times.Exactly(2));
            _channelInfo.SetupGet(e => e.TokenProvider)
                .Returns("provider")
                .Verifiable(Times.Exactly(2));
            _provider.Setup(e => e.GetKeyedService(It.IsAny<Type>(), It.IsAny<string>()))
                .Returns(_channelFactory.Object)
                .Verifiable(Times.Once);
            _connections.Setup(e => e.GetConnection(It.IsAny<string>()))
                .Returns(_token.Object)
                .Verifiable(Times.Once);
            _channelFactory.Setup(e => e.CreateChannel(It.IsAny<IAccessTokenProvider>()))
                .Returns(_channel.Object)
                .Verifiable(Times.Once);

            var host = new ConfigurationChannelHost(_provider.Object, _conversationIdFactory.Object, _connections.Object, _config, _defaultChannel);
            host.Channels.Add(_defaultChannel, _channelInfo.Object);
            var result = host.GetChannel(_defaultChannel);

            Assert.Equal(_channel.Object, result);
            Mock.Verify(_channelInfo, _provider, _connections, _channelFactory);
        }
    }
}
