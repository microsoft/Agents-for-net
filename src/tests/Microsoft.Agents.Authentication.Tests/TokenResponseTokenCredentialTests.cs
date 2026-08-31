// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Core;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Authentication.Errors;
using Microsoft.Agents.Core.Models;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Auth.Tests
{
    public class TokenResponseTokenCredentialTests
    {
        [Fact]
        public void Create_ProviderIsNull_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => TokenResponseTokenCredential.Create(null));
        }

        [Fact]
        public async Task GetTokenAsync_ForwardsRequestAndReturnsProviderToken()
        {
            string[] actualScopes = null;
            CancellationToken actualCancellationToken = default;
            var expiration = DateTimeOffset.UtcNow.AddMinutes(30);
            using var cancellationSource = new CancellationTokenSource();
            TokenCredential credential = TokenResponseTokenCredential.Create((scopes, cancellationToken) =>
            {
                actualScopes = scopes;
                actualCancellationToken = cancellationToken;
                return Task.FromResult(new TokenResponse
                {
                    Token = "token",
                    Expiration = expiration
                });
            });

            AccessToken actualToken = await credential.GetTokenAsync(
                new TokenRequestContext(["scope-1", "scope-2"]),
                cancellationSource.Token);

            Assert.Equal("token", actualToken.Token);
            Assert.Equal(expiration, actualToken.ExpiresOn);
            Assert.Equal(["scope-1", "scope-2"], actualScopes);
            Assert.Equal(cancellationSource.Token, actualCancellationToken);
        }

        [Fact]
        public void GetToken_ForwardsRequestAndReturnsProviderToken()
        {
            string[] actualScopes = null;
            CancellationToken actualCancellationToken = default;
            var expiration = DateTimeOffset.UtcNow.AddMinutes(30);
            using var cancellationSource = new CancellationTokenSource();
            TokenCredential credential = TokenResponseTokenCredential.Create((scopes, cancellationToken) =>
            {
                actualScopes = scopes;
                actualCancellationToken = cancellationToken;
                return Task.FromResult(new TokenResponse
                {
                    Token = "token",
                    Expiration = expiration
                });
            });

            AccessToken actualToken = credential.GetToken(
                new TokenRequestContext(["scope-1", "scope-2"]),
                cancellationSource.Token);

            Assert.Equal("token", actualToken.Token);
            Assert.Equal(expiration, actualToken.ExpiresOn);
            Assert.Equal(["scope-1", "scope-2"], actualScopes);
            Assert.Equal(cancellationSource.Token, actualCancellationToken);
        }

        [Fact]
        public async Task Create_MissingExpiration_UsesMinimumExpiration()
        {
            TokenCredential credential = TokenResponseTokenCredential.Create(
                (_, _) => Task.FromResult(new TokenResponse { Token = "token" }));

            AccessToken token = await credential.GetTokenAsync(new TokenRequestContext([]), CancellationToken.None);

            Assert.Equal(DateTimeOffset.MinValue, token.ExpiresOn);
        }

        [Fact]
        public async Task GetTokenAsync_ProviderReturnsNull_ThrowsFormalError()
        {
            TokenCredential credential = TokenResponseTokenCredential.Create(
                (_, _) => Task.FromResult<TokenResponse>(null));

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await credential.GetTokenAsync(new TokenRequestContext([]), CancellationToken.None));

            AssertFormalNullTokenResponseError(exception);
        }

        [Fact]
        public async Task GetTokenAsync_ResponseHasNoToken_ThrowsFormalError()
        {
            TokenCredential credential = TokenResponseTokenCredential.Create(
                (_, _) => Task.FromResult(new TokenResponse()));

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await credential.GetTokenAsync(new TokenRequestContext([]), CancellationToken.None));

            AssertFormalNullTokenResponseError(exception);
        }

        private static void AssertFormalNullTokenResponseError(InvalidOperationException exception)
        {
            Assert.Equal(ErrorHelper.NullTokenResponse.code, exception.HResult);
            Assert.Equal(ErrorHelper.NullTokenResponse.description, exception.Message);
            Assert.Equal(ErrorHelper.NullTokenResponse.helplink, exception.HelpLink);
        }
    }
}
