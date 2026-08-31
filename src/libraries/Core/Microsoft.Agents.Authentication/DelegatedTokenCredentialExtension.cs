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
    internal static class DelegatedTokenCredentialExtension
    {
        internal static TokenCredential Create(Func<string[], CancellationToken, Task<TokenResponse>> provider)
        {
            AssertionHelpers.ThrowIfNull(provider, nameof(provider));

            return Azure.Core.DelegatedTokenCredential.Create(GetToken, GetTokenAsync);

            AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                return GetTokenAsync(requestContext, cancellationToken).AsTask().GetAwaiter().GetResult();
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
