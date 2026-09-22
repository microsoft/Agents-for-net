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
    private static readonly Uri s_agentOrigin = new("https://agent.example");

    [Fact]
    public async Task GetAccessTokenAsync_None_ReturnsNull()
    {
        var resolver = new Mock<IOAuthCredentialProviderResolver>(MockBehavior.Strict);
        var oauth = new Mock<IOAuthTokenClient>(MockBehavior.Strict);
        var provider = new A2AAccessTokenProvider(resolver.Object, oauth.Object, s_agentOrigin);

        string? token = await provider.GetAccessTokenAsync(authentication: null, CancellationToken.None);

        Assert.Null(token);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ResolvesBindingBeforeAcquisition()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication();
        OAuthCredentialBinding binding = CreateBinding();
        var resolver = new Mock<IOAuthCredentialProviderResolver>(MockBehavior.Strict);
        var oauth = new Mock<IOAuthTokenClient>(MockBehavior.Strict);
        var sequence = new MockSequence();
        resolver.InSequence(sequence)
            .Setup(value => value.ResolveAsync(s_agentOrigin, authentication, It.IsAny<CancellationToken>()))
            .ReturnsAsync(binding);
        oauth.InSequence(sequence)
            .Setup(client => client.AcquireTokenAsync(binding, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OAuthAccessToken("generic-token", ExpiresIn: null));
        var provider = new A2AAccessTokenProvider(resolver.Object, oauth.Object, s_agentOrigin);

        string? token = await provider.GetAccessTokenAsync(authentication, CancellationToken.None);

        Assert.Equal("generic-token", token);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ReusesUnexpiredToken()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication();
        OAuthCredentialBinding binding = CreateBinding();
        var resolver = new Mock<IOAuthCredentialProviderResolver>(MockBehavior.Strict);
        resolver.Setup(value => value.ResolveAsync(s_agentOrigin, authentication, It.IsAny<CancellationToken>()))
            .ReturnsAsync(binding);
        var oauth = new Mock<IOAuthTokenClient>(MockBehavior.Strict);
        oauth.Setup(client => client.AcquireTokenAsync(binding, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OAuthAccessToken("generic-token", TimeSpan.FromMinutes(10)));
        var timeProvider = new TestTimeProvider();
        var provider = new A2AAccessTokenProvider(resolver.Object, oauth.Object, s_agentOrigin, timeProvider);

        Assert.Equal("generic-token", await provider.GetAccessTokenAsync(authentication, CancellationToken.None));
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal("generic-token", await provider.GetAccessTokenAsync(authentication, CancellationToken.None));

        oauth.Verify(
            client => client.AcquireTokenAsync(binding, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ReusesTokenWithoutExpiration()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication();
        OAuthCredentialBinding binding = CreateBinding();
        var resolver = new Mock<IOAuthCredentialProviderResolver>(MockBehavior.Strict);
        resolver.Setup(value => value.ResolveAsync(s_agentOrigin, authentication, It.IsAny<CancellationToken>()))
            .ReturnsAsync(binding);
        var oauth = new Mock<IOAuthTokenClient>(MockBehavior.Strict);
        oauth.Setup(client => client.AcquireTokenAsync(binding, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OAuthAccessToken("generic-token", ExpiresIn: null));
        var timeProvider = new TestTimeProvider();
        var provider = new A2AAccessTokenProvider(resolver.Object, oauth.Object, s_agentOrigin, timeProvider);

        Assert.Equal("generic-token", await provider.GetAccessTokenAsync(authentication, CancellationToken.None));
        timeProvider.Advance(TimeSpan.FromDays(365));
        Assert.Equal("generic-token", await provider.GetAccessTokenAsync(authentication, CancellationToken.None));

        oauth.Verify(
            client => client.AcquireTokenAsync(binding, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ConcurrentRequestsResolvingToSameBindingShareTokenAcquisition()
    {
        A2AAgentCardAuthentication firstAuthentication = CreateAuthentication(securitySchemeName: "github");
        A2AAgentCardAuthentication secondAuthentication = CreateAuthentication(securitySchemeName: "delegated");
        OAuthCredentialBinding binding = CreateBinding();
        var tokenSource = new TaskCompletionSource<OAuthAccessToken>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var resolver = new Mock<IOAuthCredentialProviderResolver>(MockBehavior.Strict);
        resolver.Setup(value => value.ResolveAsync(s_agentOrigin, firstAuthentication, It.IsAny<CancellationToken>()))
            .ReturnsAsync(binding);
        resolver.Setup(value => value.ResolveAsync(s_agentOrigin, secondAuthentication, It.IsAny<CancellationToken>()))
            .ReturnsAsync(binding);
        var oauth = new Mock<IOAuthTokenClient>(MockBehavior.Strict);
        oauth.Setup(client => client.AcquireTokenAsync(binding, It.IsAny<CancellationToken>()))
            .Returns(tokenSource.Task);
        var provider = new A2AAccessTokenProvider(resolver.Object, oauth.Object, s_agentOrigin);

        Task<string?> firstRequest = provider.GetAccessTokenAsync(firstAuthentication, CancellationToken.None);
        Task<string?> secondRequest = provider.GetAccessTokenAsync(secondAuthentication, CancellationToken.None);
        tokenSource.SetResult(new OAuthAccessToken("generic-token", TimeSpan.FromMinutes(10)));

        string?[] tokens = await Task.WhenAll(firstRequest, secondRequest);
        Assert.All(tokens, token => Assert.Equal("generic-token", token));
        oauth.Verify(
            client => client.AcquireTokenAsync(binding, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ExpiredToken_UsesOriginalBindingForRefresh()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication();
        OAuthCredentialBinding initialBinding = CreateBinding(
            tokenEndpoint: new Uri("https://identity.example.com/oauth/token"),
            effectiveScopes: ["agent.read", "agent.write"]);
        OAuthCredentialBinding resolvedBinding = CreateBinding(
            effectiveScopes: ["agent.write", "agent.read"],
            tokenEndpoint: new Uri("https://identity.example.com/oauth/rotated-token"));
        var resolver = new Mock<IOAuthCredentialProviderResolver>(MockBehavior.Strict);
        resolver.SetupSequence(value => value.ResolveAsync(s_agentOrigin, authentication, It.IsAny<CancellationToken>()))
            .ReturnsAsync(initialBinding)
            .ReturnsAsync(resolvedBinding);
        var oauth = new Mock<IOAuthTokenClient>(MockBehavior.Strict);
        oauth.Setup(client => client.AcquireTokenAsync(initialBinding, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OAuthAccessToken("initial-token", TimeSpan.FromMinutes(5), "refresh-token"));
        oauth.Setup(client => client.RefreshTokenAsync(initialBinding, "refresh-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OAuthAccessToken("renewed-token", TimeSpan.FromMinutes(5), "new-refresh-token"));
        var timeProvider = new TestTimeProvider();
        var provider = new A2AAccessTokenProvider(resolver.Object, oauth.Object, s_agentOrigin, timeProvider);

        Assert.Equal("initial-token", await provider.GetAccessTokenAsync(authentication, CancellationToken.None));
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal("renewed-token", await provider.GetAccessTokenAsync(authentication, CancellationToken.None));
    }

    [Fact]
    public async Task GetAccessTokenAsync_ExpiredTokenWithoutRefreshToken_Reacquires()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication();
        OAuthCredentialBinding binding = CreateBinding();
        var resolver = new Mock<IOAuthCredentialProviderResolver>(MockBehavior.Strict);
        resolver.Setup(value => value.ResolveAsync(s_agentOrigin, authentication, It.IsAny<CancellationToken>()))
            .ReturnsAsync(binding);
        var oauth = new Mock<IOAuthTokenClient>(MockBehavior.Strict);
        oauth.SetupSequence(client => client.AcquireTokenAsync(binding, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OAuthAccessToken("initial-token", TimeSpan.FromMinutes(5)))
            .ReturnsAsync(new OAuthAccessToken("renewed-token", TimeSpan.FromMinutes(5)));
        var timeProvider = new TestTimeProvider();
        var provider = new A2AAccessTokenProvider(resolver.Object, oauth.Object, s_agentOrigin, timeProvider);

        Assert.Equal("initial-token", await provider.GetAccessTokenAsync(authentication, CancellationToken.None));
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal("renewed-token", await provider.GetAccessTokenAsync(authentication, CancellationToken.None));
    }

    [Fact]
    public async Task GetAccessTokenAsync_DifferentSchemeNamesWithSameBindingShareToken()
    {
        A2AAgentCardAuthentication firstAuthentication = CreateAuthentication(securitySchemeName: "github");
        A2AAgentCardAuthentication secondAuthentication = CreateAuthentication(securitySchemeName: "delegated");
        OAuthCredentialBinding binding = CreateBinding();
        var resolver = new Mock<IOAuthCredentialProviderResolver>(MockBehavior.Strict);
        resolver.Setup(value => value.ResolveAsync(s_agentOrigin, firstAuthentication, It.IsAny<CancellationToken>()))
            .ReturnsAsync(binding);
        resolver.Setup(value => value.ResolveAsync(s_agentOrigin, secondAuthentication, It.IsAny<CancellationToken>()))
            .ReturnsAsync(binding);
        var oauth = new Mock<IOAuthTokenClient>(MockBehavior.Strict);
        oauth.Setup(client => client.AcquireTokenAsync(binding, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OAuthAccessToken("shared-token", TimeSpan.FromMinutes(10)));
        var provider = new A2AAccessTokenProvider(
            resolver.Object,
            oauth.Object,
            s_agentOrigin,
            new TestTimeProvider());

        string? firstToken = await provider.GetAccessTokenAsync(firstAuthentication, CancellationToken.None);
        string? secondToken = await provider.GetAccessTokenAsync(secondAuthentication, CancellationToken.None);

        Assert.Equal("shared-token", firstToken);
        Assert.Equal("shared-token", secondToken);
        oauth.Verify(
            client => client.AcquireTokenAsync(binding, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public Task GetAccessTokenAsync_DifferentProviderIdentitiesDoNotShareCachedTokens()
        => AssertDistinctBindingsDoNotShareCachedTokens(binding => binding with
        {
            ProviderIdentity = "provider://rotated",
        });

    [Fact]
    public Task GetAccessTokenAsync_DifferentProviderIdsDoNotShareCachedTokens()
        => AssertDistinctBindingsDoNotShareCachedTokens(binding => binding with
        {
            ProviderId = "provider-two",
        });

    [Fact]
    public Task GetAccessTokenAsync_DifferentRegistrationIdsDoNotShareCachedTokens()
        => AssertDistinctBindingsDoNotShareCachedTokens(binding => binding with
        {
            RegistrationId = "browser",
            Registration = binding.Registration with { Id = "browser" },
        });

    [Fact]
    public Task GetAccessTokenAsync_DifferentClientIdsDoNotShareCachedTokens()
        => AssertDistinctBindingsDoNotShareCachedTokens(binding => binding with
        {
            Registration = binding.Registration with { ClientId = "provider-client-id-2" },
        });

    [Fact]
    public Task GetAccessTokenAsync_DifferentFlowsDoNotShareCachedTokens()
        => AssertDistinctBindingsDoNotShareCachedTokens(binding => binding with
        {
            FlowType = A2AOAuthFlowType.ClientCredentials,
            DeviceAuthorizationEndpoint = null,
            Registration = binding.Registration with
            {
                GrantTypes = [A2AOAuthFlowType.ClientCredentials],
            },
        });

    [Fact]
    public Task GetAccessTokenAsync_DifferentScopeSetsDoNotShareCachedTokens()
        => AssertDistinctBindingsDoNotShareCachedTokens(binding => binding with
        {
            EffectiveScopes = ["agent.read", "agent.write"],
        });

    private static OAuthCredentialBinding CreateBinding(
        string providerId = "provider",
        string providerIdentity = "provider",
        string registrationId = "default",
        string clientId = "provider-client-id",
        A2AOAuthFlowType flowType = A2AOAuthFlowType.DeviceCode,
        Uri? deviceAuthorizationEndpoint = null,
        Uri? tokenEndpoint = null,
        IReadOnlyList<string>? effectiveScopes = null)
    {
        OAuthClientRegistration registration = new(
            registrationId,
            [flowType],
            clientId,
            ClientSecret: null,
            RedirectUri: null,
            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.None,
            UsePkce: true);
        return new OAuthCredentialBinding(
            providerId,
            OAuthCredentialProviderType.GenericOAuth2,
            registrationId,
            registration,
            flowType,
            AuthorizationEndpoint: null,
            DeviceAuthorizationEndpoint: flowType == A2AOAuthFlowType.DeviceCode
                ? deviceAuthorizationEndpoint ?? new Uri("https://identity.example.com/oauth/device")
                : null,
            TokenEndpoint: tokenEndpoint ?? new Uri("https://identity.example.com/oauth/token"),
            MetadataUrl: null,
            RegistrationEndpoint: null,
            EffectiveScopes: effectiveScopes ?? ["agent.read"],
            ProviderIdentity: providerIdentity);
    }

    private static async Task AssertDistinctBindingsDoNotShareCachedTokens(
        Func<OAuthCredentialBinding, OAuthCredentialBinding> mutateBinding)
    {
        A2AAgentCardAuthentication firstAuthentication = CreateAuthentication(securitySchemeName: "provider");
        A2AAgentCardAuthentication secondAuthentication = CreateAuthentication(securitySchemeName: "provider");
        OAuthCredentialBinding firstBinding = CreateBinding();
        OAuthCredentialBinding secondBinding = mutateBinding(firstBinding);
        var resolver = new Mock<IOAuthCredentialProviderResolver>(MockBehavior.Strict);
        resolver.Setup(value => value.ResolveAsync(s_agentOrigin, firstAuthentication, It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstBinding);
        resolver.Setup(value => value.ResolveAsync(s_agentOrigin, secondAuthentication, It.IsAny<CancellationToken>()))
            .ReturnsAsync(secondBinding);
        var oauth = new Mock<IOAuthTokenClient>(MockBehavior.Strict);
        oauth.Setup(client => client.AcquireTokenAsync(firstBinding, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OAuthAccessToken("first-token", TimeSpan.FromMinutes(10)));
        oauth.Setup(client => client.AcquireTokenAsync(secondBinding, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OAuthAccessToken("second-token", TimeSpan.FromMinutes(10)));
        var provider = new A2AAccessTokenProvider(
            resolver.Object,
            oauth.Object,
            s_agentOrigin,
            new TestTimeProvider());

        string? firstToken = await provider.GetAccessTokenAsync(firstAuthentication, CancellationToken.None);
        string? secondToken = await provider.GetAccessTokenAsync(secondAuthentication, CancellationToken.None);

        Assert.Equal("first-token", firstToken);
        Assert.Equal("second-token", secondToken);
        oauth.Verify(client => client.AcquireTokenAsync(firstBinding, It.IsAny<CancellationToken>()), Times.Once);
        oauth.Verify(client => client.AcquireTokenAsync(secondBinding, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static A2AAgentCardAuthentication CreateAuthentication(
        string securitySchemeName = "provider",
        string baseUri = "https://identity.example.com")
    {
        var card = new AgentCard
        {
            SecuritySchemes = new Dictionary<string, SecurityScheme>
            {
                [securitySchemeName] = new()
                {
                    OAuth2SecurityScheme = new OAuth2SecurityScheme
                    {
                        Flows = new OAuthFlows
                        {
                            DeviceCode = new()
                            {
                                DeviceAuthorizationUrl = $"{baseUri}/oauth/device",
                                TokenUrl = $"{baseUri}/oauth/token",
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
                        [securitySchemeName] = new() { List = ["agent.read"] },
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
