// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Agents.Protocols.Primitives;
using Xunit;
using Moq;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using System.Collections.Generic;

namespace Microsoft.Agents.Protocols.Connector.Tests
{
        public class RestUserTokenClientTests
        {
            private const string AppId = "test-app-id";
            private const string UserId = "user-id";
            private const string ConnectionName = "connection-name";
            private const string ChannelId = "channel-id";
            private const string MagicCode = "magic-code";
            private const string FinalRedirect = "final-redirect";
            private const string IncludeFilter = "include-filter";
            private static readonly Uri OauthEndpoint = new("https://test.endpoint");
            private readonly string[] ResourceUrls = ["https://test.url"];
            private readonly TokenExchangeRequest TokenExchangeRequest = new();
            private readonly Mock<HttpClient> MockHttpClient;
            private readonly Mock<ILogger> MockLogger;

            public RestUserTokenClientTests()
            {
                MockHttpClient = new Mock<HttpClient>();
                MockLogger = new Mock<ILogger>();
            }

            [Fact]
            public void Constructor_ShouldInstantiateCorrectly()
            {
                var client = UseClient();
                Assert.NotNull(client);
            }

            [Fact]
            public void Constructor_ShouldThrowOnNullAppId()
            {
                Assert.Throws<ArgumentNullException>(() => new RestUserTokenClient(null, OauthEndpoint, MockHttpClient.Object, MockLogger.Object));
            }

            [Fact]
            public async Task GetUserTokenAsync_ShouldThrowOnDisposed()
            {
                var client = UseClient();
                client.Dispose();
                await Assert.ThrowsAsync<ObjectDisposedException>(() => client.GetUserTokenAsync(UserId, ConnectionName, ChannelId, MagicCode, CancellationToken.None));
            }

            [Fact]
            public async Task GetUserTokenAsync_ShouldThrowOnNullUserId()
            {
                var client = UseClient();
                await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetUserTokenAsync(null, ConnectionName, ChannelId, MagicCode, CancellationToken.None));
            }

            [Fact]
            public async Task GetUserTokenAsync_ShouldThrowOnNullConnectionName()
            {
                var client = UseClient();
                await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetUserTokenAsync(UserId, null, ChannelId, MagicCode, CancellationToken.None));
            }

            [Fact]
            public async Task GetUserTokenAsync_ShouldReturnToken()
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

                var result = await client.GetUserTokenAsync(UserId, ConnectionName, ChannelId, MagicCode, CancellationToken.None);

                Assert.Equal(tokenResponse.Token, result.Token);
            }

            [Fact]
            public async Task GetSignInResourceAsync_ShouldThrowOnDisposed()
            {
                var client = UseClient();
                client.Dispose();
                await Assert.ThrowsAsync<ObjectDisposedException>(() => client.GetSignInResourceAsync(ConnectionName, new Activity(), FinalRedirect, CancellationToken.None));
            }

            [Fact]
            public async Task GetSignInResourceAsync_ShouldThrowOnNullConnectionName()
            {
                var client = UseClient();
                await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetSignInResourceAsync(null, new Activity(), FinalRedirect, CancellationToken.None));
            }

            [Fact]
            public async Task GetSignInResourceAsync_ShouldThrowOnNullActivity()
            {
                var client = UseClient();
                await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetSignInResourceAsync(ConnectionName, null, FinalRedirect, CancellationToken.None));
            }

            [Fact]
            public async Task GetSignInResourceAsync_ShouldReturnContent()
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

                Assert.NotNull(await client.GetSignInResourceAsync(ConnectionName, new Activity(), FinalRedirect, CancellationToken.None));
            }

            [Fact]
            public async Task SignOutUserAsync_ShouldThrowOnDisposed()
            {
                var client = UseClient();
                client.Dispose();
                await Assert.ThrowsAsync<ObjectDisposedException>(() => client.SignOutUserAsync(UserId, ConnectionName, ChannelId, CancellationToken.None));
            }

            [Fact]
            public async Task SignOutUserAsync_ShouldThrowOnNullUserId()
            {
                var client = UseClient();
                await Assert.ThrowsAsync<ArgumentNullException>(() => client.SignOutUserAsync(null, ConnectionName, ChannelId, CancellationToken.None));
            }

            [Fact]
            public async Task SignOutUserAsync_ShouldThrowOnNullConnectionName()
            {
                var client = UseClient();
                await Assert.ThrowsAsync<ArgumentNullException>(() => client.SignOutUserAsync(UserId, null, ChannelId, CancellationToken.None));
            }

            [Fact]
            public async Task SignOutUserAsync_ShouldCallUserTokenClientSignOutAsync()
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

                await client.SignOutUserAsync(UserId, ConnectionName, ChannelId, CancellationToken.None);

                MockHttpClient.Verify(service => service.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()), Times.Once);
            }

            [Fact]
            public async Task GetTokenStatusAsync_ShouldThrowOnDisposed()
            {
                var client = UseClient();
                client.Dispose();
                await Assert.ThrowsAsync<ObjectDisposedException>(() => client.GetTokenStatusAsync(UserId, ConnectionName, ChannelId, CancellationToken.None));
            }

            [Fact]
            public async Task GetTokenStatusAsync_ShouldThrowOnNullUserId()
            {
                var client = UseClient();
                await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetTokenStatusAsync(null, ChannelId, IncludeFilter, CancellationToken.None));
            }

            [Fact]
            public async Task GetTokenStatusAsync_ShouldThrowOnNullChannelId()
            {
                var client = UseClient();
                await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetTokenStatusAsync(UserId, null, IncludeFilter, CancellationToken.None));
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

                var status = await client.GetTokenStatusAsync(UserId, ChannelId, IncludeFilter, CancellationToken.None);

                Assert.Single(status);
                Assert.True(status[0].HasToken);
            }

            [Fact]
            public async Task GetAadTokensAsync_ShouldThrowOnDisposed()
            {
                var client = UseClient();
                client.Dispose();
                await Assert.ThrowsAsync<ObjectDisposedException>(() => client.GetAadTokensAsync(UserId, ConnectionName, ResourceUrls, ChannelId, CancellationToken.None));
            }

            [Fact]
            public async Task GetAadTokensAsync_ShouldThrowOnNullUserId()
            {
                var client = UseClient();
                await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetAadTokensAsync(null, ConnectionName, ResourceUrls, ChannelId, CancellationToken.None));
            }

            [Fact]
            public async Task GetAadTokensAsync_ShouldThrowOnNullConnectionName()
            {
                var client = UseClient();
                await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetAadTokensAsync(UserId, null, ResourceUrls, ChannelId, CancellationToken.None));
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

                var result = await client.GetAadTokensAsync(UserId, ConnectionName, ResourceUrls, ChannelId, CancellationToken.None);

                Assert.Equal(2, result.Count);
                Assert.Equal(tokens["firstToken"].Token, result["firstToken"].Token);
                Assert.Equal(tokens["secondToken"].Token, result["secondToken"].Token);
            }

            [Fact]
            public async Task ExchangeTokenAsync_ShouldThrowOnDisposed()
            {
                var client = UseClient();
                client.Dispose();
                await Assert.ThrowsAsync<ObjectDisposedException>(() => client.ExchangeTokenAsync(UserId, ConnectionName, ChannelId, TokenExchangeRequest, CancellationToken.None));
            }

            [Fact]
            public async Task ExchangeTokenAsync_ShouldThrowOnNullUserId()
            {
                var client = UseClient();
                await Assert.ThrowsAsync<ArgumentNullException>(() => client.ExchangeTokenAsync(null, ConnectionName, ChannelId, TokenExchangeRequest, CancellationToken.None));
            }

            [Fact]
            public async Task ExchangeTokenAsync_ShouldThrowOnNullConnectionName()
            {
                var client = UseClient();
                await Assert.ThrowsAsync<ArgumentNullException>(() => client.ExchangeTokenAsync(UserId, null, ChannelId, TokenExchangeRequest, CancellationToken.None));
            }

            [Fact]
            public void Constructor_ShouldDisposeTwiceCorrectly()
            {
                var client = UseClient();
                client.Dispose();
                client.Dispose();
            }

            [Fact]
            public async Task ExchangeTokenAsync_ShouldThrowOnImproperResult()
            {
                var tokenResponse = new
                {
                    Token = "test-token"
                };

                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(tokenResponse))
                };

                MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

                var client = UseClient();

                await Assert.ThrowsAsync<InvalidOperationException>(() => client.ExchangeTokenAsync(UserId, ConnectionName, ChannelId, TokenExchangeRequest, CancellationToken.None));
            }

            private RestUserTokenClient UseClient()
            {
                return new RestUserTokenClient(AppId, OauthEndpoint, MockHttpClient.Object, MockLogger.Object);
            }
        }
    }
