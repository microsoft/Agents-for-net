// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Core;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Authentication.EntraAuthSidecar.Utils;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Authentication.EntraAuthSidecar
{
    /// <summary>
    /// Azure.Core <see cref="TokenCredential"/> adapter that delegates to the sidecar.
    /// </summary>
    internal class SidecarTokenCredential(IAccessTokenProvider provider) : TokenCredential
    {
        private readonly IAccessTokenProvider _provider = provider;

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            // Synchronous path required by TokenCredential contract.
            // Use Task.Run to avoid blocking the current context on ValueTask.
            return Task.Run(() => GetTokenAsync(requestContext, cancellationToken).AsTask(), cancellationToken)
                .GetAwaiter().GetResult();
        }

        public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            List<string> scopes = [.. requestContext.Scopes];
            var token = await _provider.GetAccessTokenAsync(null, scopes, false).ConfigureAwait(false);

            // Prefer the token's own "exp" claim so Azure.Core caches/refreshes on the real lifetime;
            // SidecarTokenExpiry applies a conservative fallback for opaque tokens.
            return new AccessToken(token, SidecarTokenExpiry.Resolve(token));
        }
    }
}
