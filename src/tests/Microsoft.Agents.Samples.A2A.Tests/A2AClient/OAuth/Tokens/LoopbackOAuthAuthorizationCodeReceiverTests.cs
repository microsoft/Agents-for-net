// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Samples.A2AClient.OAuth.Tokens;
using Xunit;

namespace Microsoft.Agents.Samples.A2AClient.Tests.OAuth.Tokens;

public class LoopbackOAuthAuthorizationCodeReceiverTests
{
    [Theory]
    [InlineData("https://localhost:8400/callback/")]
    [InlineData("http://example.com:8400/callback/")]
    [InlineData("http://localhost:8400/callback")]
    public async Task ReceiveCodeAsync_IncompatibleRedirect_ThrowsBeforeOpeningBrowser(string redirectUri)
    {
        bool browserOpened = false;
        var sut = new LoopbackOAuthAuthorizationCodeReceiver(_ => browserOpened = true);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ReceiveCodeAsync(
                new Uri("https://identity.example.com/authorize"),
                new Uri(redirectUri),
                "expected-state",
                CancellationToken.None));

        Assert.Contains("HTTP loopback", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(browserOpened);
    }
}
