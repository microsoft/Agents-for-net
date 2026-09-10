// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using System;
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
            AgentDelegatedScope = mode == A2AAuthMode.Delegated ? "api://agent/.default" : null,
            AgentApplicationScope = mode == A2AAuthMode.App ? "api://agent-app/.default" : null,
        };
        var client = new MsalTokenClient(
            options,
            delegatedTokenFactory: static (_, _) => Task.FromResult("delegated-token"),
            applicationTokenFactory: static (_, _) => Task.FromResult("app-token"));

        string token = mode switch
        {
            A2AAuthMode.Delegated => await client.AcquireDelegatedTokenAsync(CancellationToken.None),
            A2AAuthMode.App => await client.AcquireApplicationTokenAsync(CancellationToken.None),
            _ => throw new InvalidOperationException("Only delegated and app modes are valid for this test."),
        };

        Assert.Equal(expectedToken, token);
    }

    [Theory]
    [InlineData("Delegated", "TenantId", "PublicClientId", "AgentDelegatedScope")]
    [InlineData("App", "TenantId", "ConfidentialClientId", "ConfidentialClientSecret", "AgentApplicationScope")]
    public async Task AcquireTokenAsync_MissingConfiguration_ThrowsInvalidOperationExceptionNamingEachMissingKey(
        string modeName,
        params string[] expectedKeys)
    {
        A2AAuthMode mode = Enum.Parse<A2AAuthMode>(modeName);
        var options = new A2AClientAuthenticationOptions();
        bool factoryInvoked = false;
        var client = new MsalTokenClient(
            options,
            delegatedTokenFactory: (_, _) =>
            {
                factoryInvoked = true;
                return Task.FromResult("delegated-token");
            },
            applicationTokenFactory: (_, _) =>
            {
                factoryInvoked = true;
                return Task.FromResult("app-token");
            });

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
            Assert.DoesNotContain(nameof(A2AClientAuthenticationOptions.AgentApplicationScope), exception.Message);
        }
        else
        {
            Assert.DoesNotContain(nameof(A2AClientAuthenticationOptions.PublicClientId), exception.Message);
            Assert.DoesNotContain(nameof(A2AClientAuthenticationOptions.AgentDelegatedScope), exception.Message);
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
