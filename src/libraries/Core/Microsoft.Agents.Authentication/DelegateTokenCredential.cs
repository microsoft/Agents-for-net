// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Core;
using Microsoft.Agents.Authentication.Errors;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;


namespace Microsoft.Agents.Authentication
{

    public delegate Task<AccessToken> GetCredential(string[] scopes, CancellationToken cancellationToken = default);

    public class DelegateTokenCredential(GetCredential provider) : TokenCredential
    {

        private readonly GetCredential _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        public static DelegateTokenCredential FromTokenResponseProvider(Func<string[], CancellationToken, Task<TokenResponse>> func)
        {
            return new DelegateTokenCredential(async (scopes, cancellationToken) =>
            {
                TokenResponse res = await func(scopes, cancellationToken).ConfigureAwait(false);
                if (res?.Token == null)
                {
                    throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.NullTokenResponse, null);
                }

                return new AccessToken(
                    res.Token,
                    res.Expiration ?? DateTimeOffset.MinValue
                );
            });
        }


        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            // Synchronous path required by TokenCredential contract. GetTokenAsync uses
            // ConfigureAwait(false) throughout, so blocking directly here is safe and avoids the
            // extra thread-pool scheduling that Task.Run would incur on every synchronous call.

            // TODO ^
            return _provider.Invoke(requestContext.Scopes, cancellationToken).GetAwaiter().GetResult();
        }

        public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            List<string> scopes = [.. requestContext.Scopes];
            return await _provider.Invoke(requestContext.Scopes, cancellationToken).ConfigureAwait(false);
        }
    }
}
