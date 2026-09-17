// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using A2A;
using Microsoft.Agents.Samples.A2AClient;
using Moq;
using Xunit;

namespace Microsoft.Agents.Samples.A2A.Tests;

public class A2AAccessTokenProviderTests
{
    [Fact]
    public async Task GetAccessTokenAsync_None_ReturnsNull()
    {
        var oauth = new Mock<IOAuthTokenClient>(MockBehavior.Strict);
        var provider = new A2AAccessTokenProvider(new A2AClientAuthenticationOptions(), oauth.Object);

        string? token = await provider.GetAccessTokenAsync(authentication: null, CancellationToken.None);

        Assert.Null(token);
    }

    [Fact]
    public async Task GetAccessTokenAsync_UsesConnectionMatchingSelectedSecurityScheme()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication();
        OAuthConnectionOptions connection = CreateConnection();
        var options = CreateOptions(connection);
        var oauth = new Mock<IOAuthTokenClient>(MockBehavior.Strict);
        oauth.Setup(client => client.AcquireTokenAsync(
                authentication,
                connection,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OAuthAccessToken("generic-token", ExpiresIn: null));
        var provider = new A2AAccessTokenProvider(options, oauth.Object);

        string? token = await provider.GetAccessTokenAsync(authentication, CancellationToken.None);

        Assert.Equal("generic-token", token);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ReusesUnexpiredToken()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication();
        OAuthConnectionOptions connection = CreateConnection();
        var oauth = new Mock<IOAuthTokenClient>(MockBehavior.Strict);
        oauth.Setup(client => client.AcquireTokenAsync(
                authentication,
                connection,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OAuthAccessToken("generic-token", TimeSpan.FromMinutes(10)));
        var timeProvider = new TestTimeProvider();
        var provider = new A2AAccessTokenProvider(CreateOptions(connection), oauth.Object, timeProvider);

        Assert.Equal("generic-token", await provider.GetAccessTokenAsync(authentication, CancellationToken.None));
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal("generic-token", await provider.GetAccessTokenAsync(authentication, CancellationToken.None));

        oauth.Verify(
            client => client.AcquireTokenAsync(authentication, connection, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ReusesTokenWithoutExpiration()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication();
        OAuthConnectionOptions connection = CreateConnection();
        var oauth = new Mock<IOAuthTokenClient>(MockBehavior.Strict);
        oauth.Setup(client => client.AcquireTokenAsync(
                authentication,
                connection,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OAuthAccessToken("generic-token", ExpiresIn: null));
        var timeProvider = new TestTimeProvider();
        var provider = new A2AAccessTokenProvider(CreateOptions(connection), oauth.Object, timeProvider);

        Assert.Equal("generic-token", await provider.GetAccessTokenAsync(authentication, CancellationToken.None));
        timeProvider.Advance(TimeSpan.FromDays(365));
        Assert.Equal("generic-token", await provider.GetAccessTokenAsync(authentication, CancellationToken.None));

        oauth.Verify(
            client => client.AcquireTokenAsync(authentication, connection, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ConcurrentRequestsShareTokenAcquisition()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication();
        OAuthConnectionOptions connection = CreateConnection();
        var tokenSource = new TaskCompletionSource<OAuthAccessToken>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var oauth = new Mock<IOAuthTokenClient>(MockBehavior.Strict);
        oauth.Setup(client => client.AcquireTokenAsync(
                authentication,
                connection,
                It.IsAny<CancellationToken>()))
            .Returns(tokenSource.Task);
        var provider = new A2AAccessTokenProvider(CreateOptions(connection), oauth.Object);

        Task<string?> firstRequest = provider.GetAccessTokenAsync(authentication, CancellationToken.None);
        Task<string?> secondRequest = provider.GetAccessTokenAsync(authentication, CancellationToken.None);
        tokenSource.SetResult(new OAuthAccessToken("generic-token", TimeSpan.FromMinutes(10)));

        string?[] tokens = await Task.WhenAll(firstRequest, secondRequest);
        Assert.All(tokens, token => Assert.Equal("generic-token", token));
        oauth.Verify(
            client => client.AcquireTokenAsync(authentication, connection, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ExpiredToken_UsesRefreshToken()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication();
        OAuthConnectionOptions connection = CreateConnection();
        var oauth = new Mock<IOAuthTokenClient>(MockBehavior.Strict);
        oauth.Setup(client => client.AcquireTokenAsync(
                authentication,
                connection,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OAuthAccessToken("initial-token", TimeSpan.FromMinutes(5), "refresh-token"));
        oauth.Setup(client => client.RefreshTokenAsync(
                authentication,
                connection,
                "refresh-token",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OAuthAccessToken("renewed-token", TimeSpan.FromMinutes(5), "new-refresh-token"));
        var timeProvider = new TestTimeProvider();
        var provider = new A2AAccessTokenProvider(CreateOptions(connection), oauth.Object, timeProvider);

        Assert.Equal("initial-token", await provider.GetAccessTokenAsync(authentication, CancellationToken.None));
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal("renewed-token", await provider.GetAccessTokenAsync(authentication, CancellationToken.None));
    }

    [Fact]
    public async Task GetAccessTokenAsync_ExpiredTokenWithoutRefreshToken_Reacquires()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication();
        OAuthConnectionOptions connection = CreateConnection();
        var oauth = new Mock<IOAuthTokenClient>(MockBehavior.Strict);
        oauth.SetupSequence(client => client.AcquireTokenAsync(
                authentication,
                connection,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OAuthAccessToken("initial-token", TimeSpan.FromMinutes(5)))
            .ReturnsAsync(new OAuthAccessToken("renewed-token", TimeSpan.FromMinutes(5)));
        var timeProvider = new TestTimeProvider();
        var provider = new A2AAccessTokenProvider(CreateOptions(connection), oauth.Object, timeProvider);

        Assert.Equal("initial-token", await provider.GetAccessTokenAsync(authentication, CancellationToken.None));
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal("renewed-token", await provider.GetAccessTokenAsync(authentication, CancellationToken.None));
    }

    private static A2AClientAuthenticationOptions CreateOptions(OAuthConnectionOptions connection)
        => new()
        {
            Connections = new Dictionary<string, OAuthConnectionOptions>
            {
                ["provider"] = connection,
            },
        };

    private static OAuthConnectionOptions CreateConnection()
        => new()
        {
            ClientId = "generic-client-id",
            AllowedOrigins = [new Uri("https://identity.example.com")],
        };

    private static A2AAgentCardAuthentication CreateAuthentication()
    {
        var card = new AgentCard
        {
            SecuritySchemes = new Dictionary<string, SecurityScheme>
            {
                ["provider"] = new()
                {
                    OAuth2SecurityScheme = new OAuth2SecurityScheme
                    {
                        Flows = new OAuthFlows
                        {
                            DeviceCode = new()
                            {
                                DeviceAuthorizationUrl = "https://identity.example.com/oauth/device",
                                TokenUrl = "https://identity.example.com/oauth/token",
                            },
                        },
                    },
                },
            },
            SecurityRequirements =
            [
                new SecurityRequirement
                {
                    Schemes = new Dictionary<string, StringList>
                    {
                        ["provider"] = new() { List = ["agent.read"] },
                    },
                },
            ],
        };

        return A2AAgentCardAuthentication.Select(card, A2AAuthMode.Delegated);
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }
}

public class A2AAuthenticationSessionTests
{
    [Fact]
    public void SetMode_UpdatesSessionMode()
    {
        var session = new A2AAuthenticationSession();

        session.SetMode(A2AAuthMode.App);

        Assert.Equal(A2AAuthMode.App, session.Mode);
    }

    [Fact]
    public void SetMode_SameMode_PreservesSelectedAuthentication()
    {
        var session = new A2AAuthenticationSession();
        A2AAgentCardAuthentication authentication = CreateDelegatedAuthentication();
        session.SetAutomaticAuthentication(authentication);

        session.SetMode(A2AAuthMode.Delegated);

        Assert.Equal(A2AAuthMode.Delegated, session.Mode);
        Assert.Equal(A2AAuthMode.Delegated, session.ModeOverride);
        Assert.Same(authentication, session.SelectedAuthentication);
    }

    [Fact]
    public void SetMode_ModeChange_ClearsSelectedAuthentication()
    {
        var session = new A2AAuthenticationSession();
        session.SetAutomaticAuthentication(CreateDelegatedAuthentication());

        session.SetMode(A2AAuthMode.App);

        Assert.Equal(A2AAuthMode.App, session.Mode);
        Assert.Equal(A2AAuthMode.App, session.ModeOverride);
        Assert.Null(session.SelectedAuthentication);
    }

    private static A2AAgentCardAuthentication CreateDelegatedAuthentication()
    {
        var card = new AgentCard
        {
            SecuritySchemes = new Dictionary<string, SecurityScheme>
            {
                ["delegated"] = new()
                {
                    OAuth2SecurityScheme = new OAuth2SecurityScheme
                    {
                        Flows = new OAuthFlows
                        {
                            DeviceCode = new()
                            {
                                DeviceAuthorizationUrl = "https://identity.example.com/oauth/device",
                                TokenUrl = "https://identity.example.com/oauth/token",
                            },
                        },
                    },
                },
            },
            SecurityRequirements =
            [
                new SecurityRequirement
                {
                    Schemes = new Dictionary<string, StringList>
                    {
                        ["delegated"] = new() { List = ["agent.read"] },
                    },
                },
            ],
        };

        return A2AAgentCardAuthentication.Select(card, A2AAuthMode.Delegated);
    }
}
