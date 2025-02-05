// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Net;
using Xunit;

namespace Microsoft.Agents.Client.Tests
{
    public class ConfigurationChannelHostTests
    {
        private readonly string _defaultChannelFactory = "HttpClient";
        private readonly string _defaultChannelAlias = "botId";
        private readonly Mock<IKeyedServiceProvider> _serviceProvider = new();
        private readonly Mock<IConnections> _connections = new();
        private readonly IConfigurationRoot _config = new ConfigurationBuilder().Build();
        private readonly Mock<IChannelInfo> _channelInfo = new();
        private readonly Mock<IChannelFactory> _channelFactory = new();
        private readonly Mock<IAccessTokenProvider> _token = new();
        private readonly Mock<IChannel> _channel = new();

        [Fact]
        public void Constructor_ShouldThrowOnNullConfigSection()
        {
            Assert.Throws<ArgumentNullException>(() => new ConfigurationChannelHost(_serviceProvider.Object, _config, _defaultChannelFactory, null));
        }

        [Fact]
        public void Constructor_ShouldThrowOnEmptyConfigSection()
        {
            Assert.Throws<ArgumentException>(() => new ConfigurationChannelHost(_serviceProvider.Object, _config, _defaultChannelFactory, string.Empty));
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullServiceProvider()
        {
            Assert.Throws<ArgumentNullException>(() => new ConfigurationChannelHost(null, _config, _defaultChannelFactory));
        }

        private IConfiguration DefaultHostConfig(string channelFactory = "factory", string clientId = "id", string tokenProvider = "provider", string serviceUrl = "http://serviceUrl.com", string endpoint = "http://endpoint.com")
        {
            var sections = new Dictionary<string, string>{
                {"ChannelHost:HostEndpoint", "hostEndpoint"},
                {"ChannelHost:HostAppId", "hostAppId"},
                {"ChannelHost:Channels:0:Alias", "botId"},
                {"ChannelHost:Channels:0:ChannelFactory", channelFactory},
                {"ChannelHost:Channels:0:ConnectionSettings:ClientId", clientId },
                {"ChannelHost:Channels:0:ConnectionSettings:TokenProvider", tokenProvider },
                {"ChannelHost:Channels:0:ConnectionSettings:ServiceUrl", serviceUrl },
                {"ChannelHost:Channels:0:ConnectionSettings:Endpoint", endpoint }
            };
            return new ConfigurationBuilder()
                .AddInMemoryCollection(sections)
                .Build();
        }

        [Fact]
        public void Constructor_ShouldSetProperties()
        {
            _channelFactory.Setup(e => e.CreateChannel(It.IsAny<IChannelHost>(), It.IsAny<IChannelInfo>()))
                .Returns(_channel.Object)
                .Verifiable(Times.Once);
            _serviceProvider.Setup(e => e.GetKeyedService(It.IsAny<Type>(), It.IsAny<string>()))
                .Returns(_channelFactory.Object)
                .Verifiable(Times.Once);

            var host = new ConfigurationChannelHost(_serviceProvider.Object, DefaultHostConfig(), _defaultChannelFactory);
            var channel = host.GetChannel("botId");
            Assert.NotNull(channel);
        }

        [Fact]
        public void GetChannel_ShouldThrowOnNullName()
        {
            var host = new ConfigurationChannelHost(_serviceProvider.Object, _config, _defaultChannelFactory);

            Assert.Throws<ArgumentException>(() => host.GetChannel(string.Empty ?? null));
        }

        [Fact]
        public void GetChannel_ShouldThrowOnEmptyName()
        {
            var host = new ConfigurationChannelHost(_serviceProvider.Object, _config, _defaultChannelFactory);

            Assert.Throws<ArgumentException>(() => host.GetChannel(string.Empty));
        }

        [Fact]
        public void GetChannel_ShouldThrowOnUnknownChannel()
        {
            var host = new ConfigurationChannelHost(_serviceProvider.Object, _config, _defaultChannelFactory);

            Assert.Throws<InvalidOperationException>(() => host.GetChannel("random"));
        }

        [Fact]
        public void GetChannel_ShouldThrowOnNullChannel()
        {
            var host = new ConfigurationChannelHost(_serviceProvider.Object, DefaultHostConfig(), _defaultChannelFactory);

            Assert.Throws<ArgumentNullException>(() => host.GetChannel(null));
        }

        [Fact]
        public void GetChannel_NullChannelFactory_Defaults()
        {
            var host = new ConfigurationChannelHost(_serviceProvider.Object, DefaultHostConfig(channelFactory: null), _defaultChannelFactory);

            Assert.Equal(_defaultChannelFactory, host.Channels[_defaultChannelAlias].ChannelFactory);
        }

        [Fact]
        public void GetChannel_ShouldThrowOnUnknownChannelFactory()
        {
            _serviceProvider.Setup(e => e.GetKeyedService(It.IsAny<Type>(), It.IsAny<string>()))
                .Returns<object>(null)
                .Verifiable(Times.Once);

            var host = new ConfigurationChannelHost(_serviceProvider.Object, DefaultHostConfig(), _defaultChannelFactory);

            Assert.Throws<InvalidOperationException>(() => host.GetChannel(_defaultChannelAlias));
        }

        /*
        [Fact]
        public void GetChannel_ShouldThrowOnNullChannelTokenProvider()
        {
            _serviceProvider.Setup(e => e.GetKeyedService(It.IsAny<Type>(), It.IsAny<string>()))
                .Returns(_channelFactory.Object)
                .Verifiable(Times.Once);

            var host = new ConfigurationChannelHost(_serviceProvider.Object, DefaultHostConfig(tokenProvider: null), _defaultChannelFactory);

            Assert.Throws<ArgumentNullException>(() => host.GetChannel(_defaultChannelAlias));
            Mock.Verify(_serviceProvider);
        }
        */

        /*
        [Fact]
        public void GetChannel_ShouldThrowOnEmptyChannelTokenProvider()
        {
            _channelInfo.SetupGet(e => e.ChannelFactory)
                .Returns("factory")
                .Verifiable(Times.Exactly(2));
            _channelInfo.SetupGet(e => e.ConnectionSettings)
                .Returns(new Dictionary<string, string> { { "TokenProvider", "" } })
                .Verifiable(Times.Once);
            _serviceProvider.Setup(e => e.GetKeyedService(It.IsAny<Type>(), It.IsAny<string>()))
                .Returns(_channelFactory.Object)
                .Verifiable(Times.Once);

            var host = new ConfigurationChannelHost(_serviceProvider.Object, DefaultHostConfig(), _defaultChannelFactory);

            Assert.Throws<ArgumentException>(() => host.GetChannel(_defaultChannelAlias));
            Mock.Verify(_channelInfo, _serviceProvider);
        }
        */

        [Fact]
        public void GetChannel_ShouldReturnChannel()
        {
            _serviceProvider.Setup(e => e.GetKeyedService(It.IsAny<Type>(), It.IsAny<string>()))
                .Returns(_channelFactory.Object)
                .Verifiable(Times.Once);

            _channelFactory.Setup(e => e.CreateChannel(It.IsAny<IChannelHost>(), It.IsAny<IChannelInfo>()))
                .Returns(_channel.Object)
                .Verifiable(Times.Once);

            var host = new ConfigurationChannelHost(_serviceProvider.Object, DefaultHostConfig(), _defaultChannelFactory);
            var result = host.GetChannel(_defaultChannelAlias);

            Assert.Equal(_channel.Object, result);
            Mock.Verify(_serviceProvider, _channelFactory);
        }
    }
}
