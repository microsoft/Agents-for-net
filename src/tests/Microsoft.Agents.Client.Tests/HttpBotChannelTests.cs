// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Client.Tests
{
    public class HttpBotChannelTests
    {
        //private readonly string _toBotId = "botid";
        //private readonly string _toBotResource = "botresource";
        //private readonly Uri _endpoint = new("http://endpoint");
        //private readonly Uri _serviceUrl = new("http://serviceUrl");
        private readonly string _conversationId = "conversationid";
        private readonly Activity _activity = new(conversation: new());
        private readonly Mock<IAccessTokenProvider> _provider = new();
        private readonly Mock<IHttpClientFactory> _factory = new();
        private readonly Mock<ILogger<HttpBotChannel>> _logger = new();
        private readonly Mock<HttpClient> _httpClient = new();
        private readonly Mock<IConnections> _connections = new();
        private readonly Mock<IChannelInfo> _channelInfo = new();

        private static IChannelInfo MockChannelInfo(string clientId = "id", string tokenProvider = "provider", string serviceUrl = "http://serviceUrl.com", string endpoint = "http://endpoint.com")
        {
            Mock<IChannelInfo> channelInfo = new();

            channelInfo.SetupGet(e => e.ChannelFactory)
                .Returns("factory");
            channelInfo.SetupGet(e => e.ConnectionSettings)
                .Returns(new Dictionary<string, string> {
                    { "ClientId", clientId },
                    { "TokenProvider", tokenProvider },
                    { "ServiceUrl", serviceUrl },
                    { "Endpoint", endpoint }
                });

            return channelInfo.Object;
        }

        [Fact]
        public void PostActivityAsync_ShouldThrowOnNullEndpoint()
        {
            Assert.Throws<ArgumentNullException>(() => new HttpBotChannel(MockChannelInfo(endpoint: null), _httpClient.Object, _provider.Object));
        }

        [Fact]
        public void PostActivityAsync_ShouldThrowOnNullServiceUrl()
        {
            Assert.Throws<ArgumentNullException>(() => new HttpBotChannel(MockChannelInfo(serviceUrl: null), _httpClient.Object, _provider.Object));
        }

        [Fact]
        public async Task PostActivityAsync_ShouldThrowOnNullConversationId()
        {
            var channel = new HttpBotChannel(MockChannelInfo(), _httpClient.Object, _provider.Object);

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                channel.SendActivityAsync(null, _activity, CancellationToken.None));
        }

        [Fact]
        public async Task PostActivityAsync_ShouldThrowOnNullActivity()
        {
            var channel = new HttpBotChannel(MockChannelInfo(), _httpClient.Object, _provider.Object);

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                channel.SendActivityAsync(_conversationId, null, CancellationToken.None));
        }

        // TODO:  Commenting these out for now because can't determine how to mock HttpClient.SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption, CancellationToken cancellationToken)
        /*
        [Fact]
        public async Task PostActivityAsync_ShouldReturnSuccessfulInvokeResponse()
        {
            var httpClient = new Mock<HttpClient>();
            var content = "{\"text\": \"testing\"}";
            var message = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) };

            _provider.Setup(e => e.GetAccessTokenAsync(It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<bool>()))
                .ReturnsAsync("token")
                .Verifiable(Times.Once);
            _factory.Setup(e => e.CreateClient(It.IsAny<string>()))
                .Returns(httpClient.Object)
                .Verifiable(Times.Once);
            httpClient.Setup(e => e.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(message)
                .Verifiable(Times.Once);

            var channel = new HttpBotChannel(MockChannelInfo(), _httpClient.Object, _provider.Object);
            var response = await channel.SendActivityAsync<object>(_conversationId, _activity, CancellationToken.None);

            Assert.Equal((int)message.StatusCode, response.Status);
            Assert.Equal(content, response.Body.ToString());
            Mock.Verify(_provider, _factory, httpClient);
        }

        [Fact]
        public async Task PostActivityAsync_ShouldReturnFailedInvokeResponse()
        {
            var content = "{\"text\": \"testing\"}";
            var httpResponse = new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(content) };

            _provider.Setup(e => e.GetAccessTokenAsync(It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<bool>()))
                .ReturnsAsync("token")
                .Verifiable(Times.Once);

            var httpClient = new Mock<HttpClient>();
            httpClient.Setup(e => e.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<HttpCompletionOption>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(httpResponse)
                .Verifiable(Times.Once);

            var channel = new HttpBotChannel(MockChannelInfo(), httpClient.Object, _provider.Object);
            var response = await channel.SendActivityAsync<object>(_conversationId, _activity, CancellationToken.None);

            Assert.Equal((int)httpResponse.StatusCode, response.Status);
            Assert.Equal(content, response.Body.ToString());
            Mock.Verify(_provider, _httpClient);
        }
        */
    }
}
