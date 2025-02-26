// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Connector.RestClients;
using Microsoft.Agents.Connector.Types;
using Microsoft.Agents.Core.Models;
using Moq;
using Xunit;

namespace Microsoft.Agents.Connector.Tests
{
    public class UserTokenRestClientTests
    {
        private static readonly Uri Endpoint = new("http://localhost");
        private const string UserId = "user-id";
        private const string ConnectionName = "connection-name";
        private const string ChannelId = "channel-id";
        private const string Code = "code";
        private const string Include = "include";
        private readonly string[] AadResourceUrls = ["resource-url"];
        private readonly TokenExchangeRequest TokenExchangeRequest = new();
        private static readonly Mock<HttpClient> MockHttpClient = new();

        [Fact]
        public void Constructor_ShouldInstantiateCorrectly()
        {
            var client = UseClient();
            Assert.NotNull(client);
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullFactory()
        {
            Assert.Throws<ArgumentNullException>(() => new RestUserTokenClient("appId", Endpoint, null, null, null));
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullEndpoint()
        {
            Assert.Throws<ArgumentNullException>(() => new RestUserTokenClient("appId", null, null, null, null, null));
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullTokenProvider()
        {
            Assert.Throws<ArgumentNullException>(() => new RestUserTokenClient("appId", Endpoint, new Mock<IHttpClientFactory>().Object, null, null));
        }

        [Fact]
        public async Task GetTokenAsync_ShouldThrowOnNullUserId()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetUserTokenAsync(null, ConnectionName, ChannelId, "code", CancellationToken.None));
        }

        [Fact]
        public async Task GetTokenAsync_ShouldThrowOnNullConnectionName()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetUserTokenAsync(UserId, null, ChannelId, "code", CancellationToken.None));
        }

        [Fact]
        public async Task GetTokenAsync_ShouldReturnToken()
        {
            var tokenResponse = new TokenResponse
            {
                Token = "test-token"
            };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(tokenResponse))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

            var client = UseClient();

            var result = await client.GetUserTokenAsync(UserId, ConnectionName, ChannelId, Code, CancellationToken.None);

            Assert.Equal(tokenResponse.Token, result.Token);
        }

        [Fact]
        public async Task GetTokenAsync_ShouldReturnNullOnNotFound()
        {
            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));
            
            var client = UseClient();

            Assert.Null(await client.GetUserTokenAsync(UserId, ConnectionName, ChannelId, Code, CancellationToken.None));
        }

        [Fact]
        public async Task GetTokenAsync_ShouldThrowOnError()
        {
            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            var client = UseClient();

            await Assert.ThrowsAsync<HttpRequestException>(() => client.GetUserTokenAsync(UserId, ConnectionName, ChannelId, Code, CancellationToken.None));
        }

        [Fact]
        public async Task SignOutAsync_ShouldThrowOnNullUserId()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.SignOutUserAsync(null, ConnectionName, ChannelId, CancellationToken.None));
        }

        [Fact]
        public async Task SignOutAsync_ShouldReturnContent()
        {
            var content = new
            {
                Body = "body"
            };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(content))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

            var client = UseClient();

            // will throw for test failure
            await client.SignOutUserAsync(UserId, ConnectionName, ChannelId, CancellationToken.None);
        }

        [Fact]
        public async Task SignOutAsync_ShouldReturnNullOnNoContent()
        {
            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NoContent));

            var client = UseClient();

            // no error should happen
            await client.SignOutUserAsync(UserId, ConnectionName, ChannelId, CancellationToken.None);
        }

        [Fact]
        public async Task SignOutAsync_ShouldThrowOnError()
        {
            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            var client = UseClient();

            await Assert.ThrowsAsync<HttpRequestException>(async () => await client.SignOutUserAsync(UserId, ConnectionName, ChannelId, CancellationToken.None));
        }

        [Fact]
        public async Task GetTokenStatusAsync_ShouldThrowOnNullUserId()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetTokenStatusAsync(null, null, null, CancellationToken.None));
        }

        [Fact]
        public async Task GetTokenStatusAsync_ShouldReturnTokenStatus()
        {
            var tokenStatus = new List<TokenStatus>
            {
                new() {
                    HasToken = true
                }
            };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(tokenStatus))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

            var client = UseClient();

            var status = await client.GetTokenStatusAsync(UserId, ConnectionName, ChannelId, CancellationToken.None);

            Assert.Single(status);
            Assert.True(status[0].HasToken);
        }

        [Fact]
        public async Task GetTokenStatusAsync_ShouldThrowOnError()
        {
            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            var client = UseClient();

            await Assert.ThrowsAsync<HttpRequestException>(() => client.GetTokenStatusAsync(UserId, ChannelId, Include, CancellationToken.None));
        }

        [Fact]
        public async Task GetAadTokensAsync_ShouldThrowOnNullUserId()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetAadTokensAsync(null, ConnectionName, AadResourceUrls, "channelId", CancellationToken.None));
        }

        [Fact]
        public async Task GetAadTokensAsync_ShouldThrowOnNullConnectionName()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetAadTokensAsync(UserId, null, AadResourceUrls, "channelId", CancellationToken.None));
        }

        [Fact]
        public async Task GetAadTokensAsync_ShouldReturnTokens()
        {
            var tokens = new Dictionary<string, TokenResponse>();
            tokens.Add("firstToken", 
            new TokenResponse
            {
                Token = "test-token1"
            });
            tokens.Add("secondToken",
            new TokenResponse
            {
                Token = "test-token2"
            });
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(tokens))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

            var client = UseClient();

            var result = await client.GetAadTokensAsync(UserId, ConnectionName, AadResourceUrls, ChannelId, CancellationToken.None);

            Assert.Equal(2, result.Count);
            Assert.Equal(tokens["firstToken"].Token, result["firstToken"].Token);
            Assert.Equal(tokens["secondToken"].Token, result["secondToken"].Token);
        }

        [Fact]
        public async Task GetAadTokensAsync_ShouldThrowOnError()
        {
            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            var client = UseClient();

            await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAadTokensAsync(UserId, ConnectionName, AadResourceUrls, ChannelId, CancellationToken.None));
        }

        [Fact]
        public async Task ExchangeAsync_ShouldThrowOnNullUserId()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.ExchangeTokenAsync(null, ConnectionName, ChannelId, TokenExchangeRequest, CancellationToken.None));
        }

        [Fact]
        public async Task ExchangeAsync_ShouldThrowOnNullConnectionName()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.ExchangeTokenAsync(UserId, null, ChannelId, TokenExchangeRequest, CancellationToken.None));
        }

        [Fact]
        public async Task ExchangeAsync_ShouldThrowOnNullChannelId()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.ExchangeTokenAsync(UserId, ConnectionName, null, TokenExchangeRequest, CancellationToken.None));
        }

        [Fact]
        public async Task ExchangeAsync_ShouldThrowOnNullExchangeRequest()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.ExchangeTokenAsync(UserId, ConnectionName, ChannelId, null, CancellationToken.None));
        }

        [Fact]
        public async Task ExchangeAsync_ShouldReturnContent()
        {
            var tokenResponse = new TokenResponse
            {
                Token = "test-token"
            };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(tokenResponse))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

            var client = UseClient();

            Assert.NotNull(await client.ExchangeTokenAsync(UserId, ConnectionName, ChannelId, TokenExchangeRequest, CancellationToken.None));
        }

        [Fact]
        public async Task ExchangeAsync_ShouldThrowOnError()
        {
            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            var client = UseClient();

            await Assert.ThrowsAsync<HttpRequestException>(() => client.ExchangeTokenAsync(UserId, ConnectionName, ChannelId, TokenExchangeRequest, CancellationToken.None));
        }

        [Fact]
        public async Task ExchangeTokenAsync_ShouldReturnContent()
        {
            var tokenResponse = new TokenResponse
            {
                Token = "test-token",
                ConnectionName = ConnectionName
            };

            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(tokenResponse))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(httpResponse);

            var client = UseClient();

            var response = await client.ExchangeTokenAsync(UserId, ConnectionName, ChannelId, TokenExchangeRequest, CancellationToken.None);
            Assert.NotNull(response);
            Assert.Equal(ConnectionName, ((TokenResponse)response).ConnectionName);
            Assert.Equal("test-token", ((TokenResponse)response).Token);
        }

        [Fact]
        public async Task ExchangeTokenAsync_ShouldThrowOnError()
        {
            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            var client = UseClient();

            await Assert.ThrowsAsync<HttpRequestException>(() => client.ExchangeTokenAsync(UserId, ConnectionName, ChannelId, TokenExchangeRequest, CancellationToken.None));
        }

        [Fact]
        public async Task ExchangeTokenAsync_ErrorResponseFor400()
        {
            var errorResponse = new
            {
                Error = new Error { Message = "Error Message" }
            };

            var httpResponse = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(JsonSerializer.Serialize(errorResponse))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(httpResponse);

            var client = UseClient();

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await client.ExchangeTokenAsync(UserId, ConnectionName, ChannelId, TokenExchangeRequest, CancellationToken.None));
        }

        [Fact]
        public async Task ExchangeTokenAsync_WithContent404()
        {
            var tokenResponse = new
            {
                ConnectionName = ConnectionName
            };

            var httpResponse = new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent(JsonSerializer.Serialize(tokenResponse))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(httpResponse);

            var client = UseClient();

            var response = await client.ExchangeTokenAsync(UserId, ConnectionName, ChannelId, TokenExchangeRequest, CancellationToken.None);
            Assert.IsAssignableFrom<TokenResponse>(response);
            Assert.Equal(ConnectionName, ((TokenResponse)response).ConnectionName);
        }

        [Fact]
        public async Task ExchangeTokenAsync_WithoutContent404()
        {
            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

            var client = UseClient();

            var response = await client.ExchangeTokenAsync(UserId, ConnectionName, ChannelId, TokenExchangeRequest, CancellationToken.None);
            Assert.Null(response);
        }

        private static RestUserTokenClient UseClient()
        {
            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory.Setup(a => a.CreateClient(It.IsAny<string>()))
                .Returns(MockHttpClient.Object);

            return new RestUserTokenClient("appId", Endpoint, httpFactory.Object, () => Task.FromResult<string>("test"));
        }
    }
}
