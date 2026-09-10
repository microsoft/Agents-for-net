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
    [InlineData("Delegated", "PublicClientId")]
    [InlineData("App", "ConfidentialClientId")]
    public void Validate_MissingRequiredValueForMode_ThrowsArgumentException(string modeName, string expectedParameterName)
    {
        A2AAuthMode mode = Enum.Parse<A2AAuthMode>(modeName);
        var options = new A2AClientAuthenticationOptions
        {
            TenantId = "tenant-id",
            PublicClientId = mode == A2AAuthMode.Delegated ? string.Empty : "public-client-id",
            ConfidentialClientId = mode == A2AAuthMode.App ? string.Empty : "confidential-client-id",
            ConfidentialClientSecret = "secret",
            AgentDelegatedScope = "api://agent/.default",
            AgentApplicationScope = "api://agent-app/.default",
        };

        var exception = Assert.Throws<ArgumentException>(() => options.Validate(mode));

        Assert.Equal(expectedParameterName, exception.ParamName);
    }

    [Fact]
    public void Validate_None_DoesNotRequireAuthenticationValues()
    {
        var options = new A2AClientAuthenticationOptions
        {
            TenantId = string.Empty,
            PublicClientId = string.Empty,
            ConfidentialClientId = string.Empty,
            ConfidentialClientSecret = string.Empty,
            AgentDelegatedScope = string.Empty,
            AgentApplicationScope = string.Empty,
        };

        options.Validate(A2AAuthMode.None);
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
