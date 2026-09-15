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
        var provider = new A2AAccessTokenProvider(msal.Object);

        string? token = await provider.GetAccessTokenAsync(A2AAuthMode.None, CancellationToken.None);

        Assert.Null(token);
    }

    [Theory]
    [InlineData("Delegated", "delegated-token")]
    [InlineData("App", "app-token")]
    public async Task GetAccessTokenAsync_UsesSelectedFlow(string modeName, string expected)
    {
        var msal = new Mock<IMsalTokenClient>();
        msal.Setup(client => client.AcquireDelegatedTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("delegated-token");
        msal.Setup(client => client.AcquireApplicationTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("app-token");
        var provider = new A2AAccessTokenProvider(msal.Object);
        A2AAuthMode mode = Enum.Parse<A2AAuthMode>(modeName);

        string? token = await provider.GetAccessTokenAsync(mode, CancellationToken.None);

        Assert.Equal(expected, token);
    }

    [Theory]
    [InlineData("Delegated", "Device Code")]
    [InlineData("App", "Client Credentials")]
    public async Task GetAccessTokenAsync_CardWithoutOAuth_DefersFailureUntilTokenRequested(string modeName, string expectedFlow)
    {
        var msal = new MsalTokenClient(new A2AClientAuthenticationOptions());
        msal.Configure(A2AClientProgramTests.CreateCard("none"));
        var provider = new A2AAccessTokenProvider(msal);

        Assert.Null(await provider.GetAccessTokenAsync(A2AAuthMode.None, CancellationToken.None));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GetAccessTokenAsync(Enum.Parse<A2AAuthMode>(modeName), CancellationToken.None));

        Assert.Contains($"{expectedFlow} authentication", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Missing required", exception.Message, StringComparison.Ordinal);
        Assert.Null(await provider.GetAccessTokenAsync(A2AAuthMode.None, CancellationToken.None));
    }

    [Theory]
    [InlineData("delegated", "Delegated", "api://agent/access_as_user", "https://login.microsoftonline.com/organizations")]
    [InlineData("app", "App", "api://agent/.default", "https://login.microsoftonline.com/tenant-id")]
    [InlineData("both", "Delegated", "api://agent/access_as_user", "https://login.microsoftonline.com/organizations")]
    [InlineData("both", "App", "api://agent/.default", "https://login.microsoftonline.com/tenant-id")]
    public async Task GetAccessTokenAsync_ConfiguresOnlyRequestedCardFlow(
        string cardFlows,
        string modeName,
        string expectedScope,
        string expectedAuthority)
    {
        A2AAuthMode mode = Enum.Parse<A2AAuthMode>(modeName);
        var options = new A2AClientAuthenticationOptions
        {
            TenantId = "tenant-id",
            PublicClientId = mode == A2AAuthMode.Delegated ? "public-client-id" : null,
            ConfidentialClientId = mode == A2AAuthMode.App ? "confidential-client-id" : null,
            ConfidentialClientSecret = mode == A2AAuthMode.App ? "test-secret" : null,
        };
        var acquiredModes = new List<A2AAuthMode>();
        MsalTokenAcquisitionRequest? acquisition = null;
        CancellationToken acquiredCancellation = default;
        var msal = new MsalTokenClient(
            options,
            delegatedTokenFactory: (_, request, cancellationToken) =>
            {
                acquiredModes.Add(A2AAuthMode.Delegated);
                acquisition = request;
                acquiredCancellation = cancellationToken;
                return Task.FromResult("delegated-token");
            },
            applicationTokenFactory: (_, request, cancellationToken) =>
            {
                acquiredModes.Add(A2AAuthMode.App);
                acquisition = request;
                acquiredCancellation = cancellationToken;
                return Task.FromResult("app-token");
            });
        msal.Configure(A2AClientProgramTests.CreateCard(cardFlows));
        var provider = new A2AAccessTokenProvider(msal);
        using var cancellation = new CancellationTokenSource();

        Assert.Null(await provider.GetAccessTokenAsync(A2AAuthMode.None, cancellation.Token));
        Assert.Empty(acquiredModes);
        string? token = await provider.GetAccessTokenAsync(mode, cancellation.Token);

        Assert.Equal(mode == A2AAuthMode.Delegated ? "delegated-token" : "app-token", token);
        Assert.Equal([mode], acquiredModes);
        Assert.Equal(cancellation.Token, acquiredCancellation);
        Assert.NotNull(acquisition);
        Assert.Equal([expectedScope], acquisition.Scopes);
        Assert.Equal(new Uri(expectedAuthority), acquisition.Authority);
    }

    [Theory]
    [InlineData("Delegated", "App", "absolute HTTPS token endpoint")]
    [InlineData("App", "Delegated", "device authorization endpoint")]
    public async Task GetAccessTokenAsync_InvalidOtherFlow_DoesNotBlockSupportedMode(
        string supportedModeName,
        string invalidModeName,
        string expectedFailure)
    {
        var card = A2AClientProgramTests.CreateCard("both");
        if (invalidModeName == "App")
        {
            card.SecuritySchemes!["application"].OAuth2SecurityScheme!.Flows!.ClientCredentials!.TokenUrl = "http://login.example/token";
        }
        else
        {
            card.SecuritySchemes!["delegated"].OAuth2SecurityScheme!.Flows!.DeviceCode!.DeviceAuthorizationUrl =
                "https://other.example/organizations/oauth2/v2.0/devicecode";
        }
        var msal = new MsalTokenClient(
            new A2AClientAuthenticationOptions
            {
                TenantId = "tenant-id",
                PublicClientId = "public-client-id",
                ConfidentialClientId = "confidential-client-id",
                ConfidentialClientSecret = "test-secret",
            },
            delegatedTokenFactory: (_, _, _) => Task.FromResult("delegated-token"),
            applicationTokenFactory: (_, _, _) => Task.FromResult("app-token"));
        msal.Configure(card);
        var provider = new A2AAccessTokenProvider(msal);
        A2AAuthMode supportedMode = Enum.Parse<A2AAuthMode>(supportedModeName);
        string expectedToken = supportedMode == A2AAuthMode.Delegated ? "delegated-token" : "app-token";

        Assert.Null(await provider.GetAccessTokenAsync(A2AAuthMode.None, CancellationToken.None));
        Assert.Equal(expectedToken, await provider.GetAccessTokenAsync(supportedMode, CancellationToken.None));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GetAccessTokenAsync(Enum.Parse<A2AAuthMode>(invalidModeName), CancellationToken.None));

        Assert.Contains(expectedFailure, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(expectedToken, await provider.GetAccessTokenAsync(supportedMode, CancellationToken.None));
        Assert.Null(await provider.GetAccessTokenAsync(A2AAuthMode.None, CancellationToken.None));
    }
}

public class A2AClientAuthenticationOptionsTests
{
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
}
