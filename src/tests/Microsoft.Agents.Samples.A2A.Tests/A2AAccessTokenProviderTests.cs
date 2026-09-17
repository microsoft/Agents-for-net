// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System;
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
        var msal = new Mock<IMsalTokenClient>(MockBehavior.Strict);
        var github = new Mock<IGitHubDeviceFlowTokenClient>(MockBehavior.Strict);
        var provider = new A2AAccessTokenProvider(msal.Object, github.Object);

        string? token = await provider.GetAccessTokenAsync(authentication: null, CancellationToken.None);

        Assert.Null(token);
    }

    [Theory]
    [InlineData("Delegated", "delegated-token")]
    [InlineData("App", "app-token")]
    public async Task GetAccessTokenAsync_UsesSelectedFlow(string modeName, string expected)
    {
        A2AAuthMode mode = Enum.Parse<A2AAuthMode>(modeName);
        A2AAgentCardAuthentication authentication = CreateAuthentication(mode);
        var msal = new Mock<IMsalTokenClient>(MockBehavior.Strict);
        var github = new Mock<IGitHubDeviceFlowTokenClient>(MockBehavior.Strict);
        msal.Setup(client => client.AcquireDelegatedTokenAsync(
                It.Is<A2AAgentCardAuthentication>(candidate => candidate == authentication),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("delegated-token");
        msal.Setup(client => client.AcquireApplicationTokenAsync(
                It.Is<A2AAgentCardAuthentication>(candidate => candidate == authentication),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("app-token");
        var provider = new A2AAccessTokenProvider(msal.Object, github.Object);

        string? token = await provider.GetAccessTokenAsync(authentication, CancellationToken.None);

        Assert.Equal(expected, token);
    }

    [Fact]
    public async Task GetAccessTokenAsync_DelegatedSelection_ReentersMsalAndReturnsRenewedToken()
    {
        A2AAgentCardAuthentication authentication = CreateAuthentication(A2AAuthMode.Delegated);
        var msal = new Mock<IMsalTokenClient>(MockBehavior.Strict);
        var github = new Mock<IGitHubDeviceFlowTokenClient>(MockBehavior.Strict);
        msal.SetupSequence(client => client.AcquireDelegatedTokenAsync(
                authentication,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("initial-token")
            .ReturnsAsync("renewed-token");
        var provider = new A2AAccessTokenProvider(msal.Object, github.Object);

        Assert.Equal("initial-token", await provider.GetAccessTokenAsync(authentication, CancellationToken.None));
        Assert.Equal("renewed-token", await provider.GetAccessTokenAsync(authentication, CancellationToken.None));
    }

    [Fact]
    public async Task GetAccessTokenAsync_GitHubSelection_ReusesNonExpiringToken()
    {
        var msal = new Mock<IMsalTokenClient>(MockBehavior.Strict);
        var github = new Mock<IGitHubDeviceFlowTokenClient>(MockBehavior.Strict);
        A2AAgentCardAuthentication auth = CreateGitHubAuthentication();
        github.Setup(client => client.AcquireTokenAsync(auth, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitHubDeviceFlowAccessToken("github-token", ExpiresIn: null));
        var timeProvider = new TestTimeProvider();
        var provider = new A2AAccessTokenProvider(msal.Object, github.Object, timeProvider);

        Assert.Equal("github-token", await provider.GetAccessTokenAsync(auth, CancellationToken.None));
        timeProvider.Advance(TimeSpan.FromDays(365));
        Assert.Equal("github-token", await provider.GetAccessTokenAsync(auth, CancellationToken.None));

        github.Verify(client => client.AcquireTokenAsync(auth, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAccessTokenAsync_GitHubSelection_ReusesUnexpiredToken()
    {
        var msal = new Mock<IMsalTokenClient>(MockBehavior.Strict);
        var github = new Mock<IGitHubDeviceFlowTokenClient>(MockBehavior.Strict);
        A2AAgentCardAuthentication auth = CreateGitHubAuthentication();
        github.Setup(client => client.AcquireTokenAsync(auth, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitHubDeviceFlowAccessToken("github-token", TimeSpan.FromMinutes(10)));
        var timeProvider = new TestTimeProvider();
        var provider = new A2AAccessTokenProvider(msal.Object, github.Object, timeProvider);

        Assert.Equal("github-token", await provider.GetAccessTokenAsync(auth, CancellationToken.None));
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal("github-token", await provider.GetAccessTokenAsync(auth, CancellationToken.None));

        github.Verify(client => client.AcquireTokenAsync(auth, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAccessTokenAsync_GitHubSelection_ReacquiresExpiredToken()
    {
        var msal = new Mock<IMsalTokenClient>(MockBehavior.Strict);
        var github = new Mock<IGitHubDeviceFlowTokenClient>(MockBehavior.Strict);
        A2AAgentCardAuthentication auth = CreateGitHubAuthentication();
        github.SetupSequence(client => client.AcquireTokenAsync(auth, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitHubDeviceFlowAccessToken("initial-token", TimeSpan.FromMinutes(5)))
            .ReturnsAsync(new GitHubDeviceFlowAccessToken("renewed-token", ExpiresIn: null));
        var timeProvider = new TestTimeProvider();
        var provider = new A2AAccessTokenProvider(msal.Object, github.Object, timeProvider);

        Assert.Equal("initial-token", await provider.GetAccessTokenAsync(auth, CancellationToken.None));
        timeProvider.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal("renewed-token", await provider.GetAccessTokenAsync(auth, CancellationToken.None));
    }

    private static A2AAgentCardAuthentication CreateAuthentication(A2AAuthMode mode)
    {
        string schemeName = mode == A2AAuthMode.Delegated ? "delegated" : "application";
        var flows = new OAuthFlows
        {
            DeviceCode = mode == A2AAuthMode.Delegated
                ? new()
                {
                    DeviceAuthorizationUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode",
                    TokenUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/token",
                }
                : null,
            ClientCredentials = mode == A2AAuthMode.App
                ? new()
                {
                    TokenUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/token",
                }
                : null,
        };
        var card = new AgentCard
        {
            SecuritySchemes = new Dictionary<string, SecurityScheme>
            {
                [schemeName] = new()
                {
                    OAuth2SecurityScheme = new OAuth2SecurityScheme { Flows = flows },
                },
            },
            SecurityRequirements =
            [
                new SecurityRequirement
                {
                    Schemes = new Dictionary<string, StringList>
                    {
                        [schemeName] = new()
                        {
                            List = [mode == A2AAuthMode.Delegated ? "api://agent/access_as_user" : "api://agent/.default"],
                        },
                    },
                },
            ],
        };

        return A2AAgentCardAuthentication.Select(card, mode);
    }

    private static AgentCard CreateTwoProviderCard()
    {
        return new AgentCard
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
                                DeviceAuthorizationUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode",
                                TokenUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/token",
                            },
                        },
                    },
                },
                ["github"] = new()
                {
                    OAuth2SecurityScheme = new OAuth2SecurityScheme
                    {
                        Flows = new OAuthFlows
                        {
                            DeviceCode = new()
                            {
                                DeviceAuthorizationUrl = "https://github.com/login/device/code",
                                TokenUrl = "https://github.com/login/oauth/access_token",
                            },
                        },
                    },
                },
            },
            Skills =
            [
                new AgentSkill
                {
                    Id = "Microsoft Graph profile",
                    Name = "Microsoft Graph profile",
                    Examples = ["-me"],
                    SecurityRequirements =
                    [
                        new SecurityRequirement
                        {
                            Schemes = new Dictionary<string, StringList>
                            {
                                ["delegated"] = new() { List = ["api://agent/access_as_user"] },
                            },
                        },
                    ],
                },
                new AgentSkill
                {
                    Id = "GitHub assigned issues",
                    Name = "GitHub assigned issues",
                    Examples = ["-issues"],
                    SecurityRequirements =
                    [
                        new SecurityRequirement
                        {
                            Schemes = new Dictionary<string, StringList>
                            {
                                ["github"] = new() { List = ["repo"] },
                            },
                        },
                    ],
                },
            ],
        };
    }

    private static A2AAgentCardAuthentication CreateGitHubAuthentication()
    {
        AgentCard card = CreateTwoProviderCard();
        AgentSkill skill = card.Skills![1];
        return A2AAgentCardAuthentication.Select(card, skill, A2AAuthMode.Delegated)!;
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration)
        {
            _utcNow += duration;
        }
    }
}

public class A2AClientAuthenticationOptionsTests
{
    [Fact]
    public void MsalTokenClient_DoesNotExposeWholeCardConfigureOverload()
    {
        Assert.Null(typeof(MsalTokenClient).GetMethod(nameof(MsalTokenClient.Configure), [typeof(AgentCard)]));
    }

    [Theory]
    [InlineData("Delegated", "delegated-token")]
    [InlineData("App", "app-token")]
    public async Task AcquireTokenAsync_DoesNotRequireOtherModeConfiguration(string modeName, string expectedToken)
    {
        A2AAuthMode mode = Enum.Parse<A2AAuthMode>(modeName);
        var options = new A2AClientAuthenticationOptions
        {
            TenantId = "tenant-id",
            PublicClientId = mode == A2AAuthMode.Delegated ? "public-client-id" : null,
            ConfidentialClientId = mode == A2AAuthMode.App ? "confidential-client-id" : null,
            ConfidentialClientSecret = mode == A2AAuthMode.App ? "secret" : null,
        };
        var client = new MsalTokenClient(
            options,
            delegatedTokenFactory: static (_, _, _) => Task.FromResult("delegated-token"),
            applicationTokenFactory: static (_, _, _) => Task.FromResult("app-token"));
        client.Configure(CreateAuthentication(mode));

        string token = mode switch
        {
            A2AAuthMode.Delegated => await client.AcquireDelegatedTokenAsync(CancellationToken.None),
            A2AAuthMode.App => await client.AcquireApplicationTokenAsync(CancellationToken.None),
            _ => throw new InvalidOperationException("Only delegated and app modes are valid for this test."),
        };

        Assert.Equal(expectedToken, token);
    }

    [Theory]
    [InlineData("Delegated", "TenantId", "PublicClientId")]
    [InlineData("App", "TenantId", "ConfidentialClientId", "ConfidentialClientSecret")]
    public async Task AcquireTokenAsync_MissingConfiguration_ThrowsInvalidOperationExceptionNamingEachMissingKey(
        string modeName,
        params string[] expectedKeys)
    {
        A2AAuthMode mode = Enum.Parse<A2AAuthMode>(modeName);
        var options = new A2AClientAuthenticationOptions();
        bool factoryInvoked = false;
        var client = new MsalTokenClient(
            options,
            delegatedTokenFactory: (_, _, _) =>
            {
                factoryInvoked = true;
                return Task.FromResult("delegated-token");
            },
            applicationTokenFactory: (_, _, _) =>
            {
                factoryInvoked = true;
                return Task.FromResult("app-token");
            });
        client.Configure(CreateAuthentication(mode));

        InvalidOperationException exception = mode switch
        {
            A2AAuthMode.Delegated => await Assert.ThrowsAsync<InvalidOperationException>(() => client.AcquireDelegatedTokenAsync(CancellationToken.None)),
            A2AAuthMode.App => await Assert.ThrowsAsync<InvalidOperationException>(() => client.AcquireApplicationTokenAsync(CancellationToken.None)),
            _ => throw new InvalidOperationException("Only delegated and app modes are valid for this test."),
        };

        Assert.False(factoryInvoked);

        foreach (string expectedKey in expectedKeys)
        {
            Assert.Contains(expectedKey, exception.Message);
        }

        if (mode == A2AAuthMode.Delegated)
        {
            Assert.DoesNotContain(nameof(A2AClientAuthenticationOptions.ConfidentialClientId), exception.Message);
            Assert.DoesNotContain(nameof(A2AClientAuthenticationOptions.ConfidentialClientSecret), exception.Message);
        }
        else
        {
            Assert.DoesNotContain(nameof(A2AClientAuthenticationOptions.PublicClientId), exception.Message);
        }
    }

    private static A2AAgentCardAuthentication CreateAuthentication(A2AAuthMode mode)
    {
        string schemeName = mode == A2AAuthMode.Delegated ? "delegated" : "application";
        var flows = new OAuthFlows
        {
            DeviceCode = mode == A2AAuthMode.Delegated
                ? new()
                {
                    DeviceAuthorizationUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode",
                    TokenUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/token",
                }
                : null,
            ClientCredentials = mode == A2AAuthMode.App
                ? new()
                {
                    TokenUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/token",
                }
                : null,
        };
        var card = new AgentCard
        {
            SecuritySchemes = new Dictionary<string, SecurityScheme>
            {
                [schemeName] = new()
                {
                    OAuth2SecurityScheme = new OAuth2SecurityScheme { Flows = flows },
                },
            },
            SecurityRequirements =
            [
                new SecurityRequirement
                {
                    Schemes = new Dictionary<string, StringList>
                    {
                        [schemeName] = new()
                        {
                            List = [mode == A2AAuthMode.Delegated ? "api://agent/access_as_user" : "api://agent/.default"],
                        },
                    },
                },
            ],
        };

        return A2AAgentCardAuthentication.Select(card, mode);
    }

    private static AgentCard CreateTwoProviderCard()
    {
        return new AgentCard
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
                                DeviceAuthorizationUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode",
                                TokenUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/token",
                            },
                        },
                    },
                },
                ["github"] = new()
                {
                    OAuth2SecurityScheme = new OAuth2SecurityScheme
                    {
                        Flows = new OAuthFlows
                        {
                            DeviceCode = new()
                            {
                                DeviceAuthorizationUrl = "https://github.com/login/device/code",
                                TokenUrl = "https://github.com/login/oauth/access_token",
                            },
                        },
                    },
                },
            },
            Skills =
            [
                new AgentSkill
                {
                    Id = "Microsoft Graph profile",
                    Name = "Microsoft Graph profile",
                    Examples = ["-me"],
                    SecurityRequirements =
                    [
                        new SecurityRequirement
                        {
                            Schemes = new Dictionary<string, StringList>
                            {
                                ["delegated"] = new() { List = ["api://agent/access_as_user"] },
                            },
                        },
                    ],
                },
                new AgentSkill
                {
                    Id = "GitHub assigned issues",
                    Name = "GitHub assigned issues",
                    Examples = ["-issues"],
                    SecurityRequirements =
                    [
                        new SecurityRequirement
                        {
                            Schemes = new Dictionary<string, StringList>
                            {
                                ["github"] = new() { List = ["repo"] },
                            },
                        },
                    ],
                },
            ],
        };
    }

    private static A2AAgentCardAuthentication CreateGitHubAuthentication()
    {
        AgentCard card = CreateTwoProviderCard();
        AgentSkill skill = card.Skills![1];
        return A2AAgentCardAuthentication.Select(card, skill, A2AAuthMode.Delegated)!;
    }

    [Fact]
    public async Task AcquireDelegatedTokenAsync_PassesCardScopesAndAuthorityToAcquisitionPath()
    {
        MsalTokenAcquisitionRequest? request = null;
        var client = new MsalTokenClient(
            new A2AClientAuthenticationOptions
            {
                TenantId = "tenant-id",
                PublicClientId = "public-client-id",
            },
            delegatedTokenFactory: (_, acquisitionRequest, _) =>
            {
                request = acquisitionRequest;
                return Task.FromResult("delegated-token");
            },
            applicationTokenFactory: null);
        client.Configure(CreateAuthentication(A2AAuthMode.Delegated));

        string token = await client.AcquireDelegatedTokenAsync(CancellationToken.None);

        Assert.Equal("delegated-token", token);
        Assert.NotNull(request);
        Assert.Equal(["api://agent/access_as_user"], request.Scopes);
        Assert.Equal(
            new Uri("https://login.microsoftonline.com/organizations"),
            request.Authority);
        Assert.Equal(
            new Uri("https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode"),
            request.DeviceAuthorizationUrl);
    }

    [Fact]
    public async Task AcquireDelegatedTokenAsync_TwoProviderSelection_UsesSelectedAuthenticationOnly()
    {
        MsalTokenAcquisitionRequest? request = null;
        var client = new MsalTokenClient(
            new A2AClientAuthenticationOptions
            {
                TenantId = "tenant-id",
                PublicClientId = "public-client-id",
            },
            delegatedTokenFactory: (_, acquisitionRequest, _) =>
            {
                request = acquisitionRequest;
                return Task.FromResult("delegated-token");
            },
            applicationTokenFactory: null);
        AgentCard card = CreateTwoProviderCard();
        client.Configure(A2AAgentCardAuthentication.Select(card, card.Skills![0], A2AAuthMode.Delegated)!);

        string token = await client.AcquireDelegatedTokenAsync(CancellationToken.None);

        Assert.Equal("delegated-token", token);
        Assert.NotNull(request);
        Assert.Equal(["api://agent/access_as_user"], request.Scopes);
        Assert.Equal(
            new Uri("https://login.microsoftonline.com/organizations"),
            request.Authority);
        Assert.Equal(
            new Uri("https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode"),
            request.DeviceAuthorizationUrl);
    }

    [Theory]
    [InlineData("https://login.microsoftonline.com/organizations/devicecode", "path")]
    [InlineData("https://login.microsoftonline.com/common/oauth2/v2.0/devicecode", "tenant path")]
    [InlineData("https://attacker.example/organizations/oauth2/v2.0/devicecode", "authority")]
    [InlineData("https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode?ignored=true", "path")]
    public void Configure_DelegatedDeviceEndpointIncompatibleWithTokenAuthority_Throws(string deviceAuthorizationUrl, string? expectedReason)
    {
        var client = new MsalTokenClient(new A2AClientAuthenticationOptions());
        AgentCard card = new()
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
                                TokenUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/token",
                                DeviceAuthorizationUrl = deviceAuthorizationUrl,
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
                        ["delegated"] = new() { List = ["api://agent/access_as_user"] },
                    },
                },
            ],
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => client.Configure(A2AAgentCardAuthentication.Select(card, A2AAuthMode.Delegated)));

        Assert.Contains("device authorization endpoint", exception.Message, StringComparison.OrdinalIgnoreCase);
        if (expectedReason is not null)
        {
            Assert.Contains(expectedReason, exception.Message, StringComparison.OrdinalIgnoreCase);
        }
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
        AgentCard card = new()
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
                                DeviceAuthorizationUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode",
                                TokenUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/token",
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
                        ["delegated"] = new() { List = ["api://agent/access_as_user"] },
                    },
                },
            ],
        };

        return A2AAgentCardAuthentication.Select(card, A2AAuthMode.Delegated);
    }
}
