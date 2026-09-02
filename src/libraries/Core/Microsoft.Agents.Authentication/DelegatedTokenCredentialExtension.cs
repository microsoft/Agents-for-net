// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Core;
using Microsoft.Agents.Authentication.Errors;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using System;
using System.Threading;
using System.Threading.Tasks;


namespace Microsoft.Agents.Authentication
{
    /// <summary>
    /// Creates Azure credentials backed by an asynchronous Agents SDK token provider.
    /// </summary>
    internal static class DelegatedTokenCredentialExtension
    {
        /// <summary>
        /// Creates a credential that forwards token requests to <paramref name="provider"/>.
        /// </summary>
        /// <param name="provider">The asynchronous provider used to acquire a token for the requested scopes.</param>
        /// <returns>A credential backed by the supplied provider.</returns>
        internal static TokenCredential Create(Func<string[], CancellationToken, Task<TokenResponse>> provider)
        {
            AssertionHelpers.ThrowIfNull(provider, nameof(provider));

            return DelegatedTokenCredential.Create(GetToken, GetTokenAsync);

            AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                // Isolate the asynchronous provider from a caller's synchronization context before blocking.
                return Task.Run(() => GetTokenAsync(requestContext, cancellationToken).AsTask()).GetAwaiter().GetResult();
            }

            async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                TokenResponse response = await provider(requestContext.Scopes, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrEmpty(response?.Token))
                {
                    throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.NullTokenResponse, null);
                }

                return new AccessToken(
                    response.Token,
                    response.Expiration ?? DateTimeOffset.MinValue
                );
            }
        }
    }
}
