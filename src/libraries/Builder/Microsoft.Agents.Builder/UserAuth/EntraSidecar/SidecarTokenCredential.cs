// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Core;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.UserAuth.EntraSidecar
{
    /// <summary>
    /// Azure.Core <see cref="TokenCredential"/> adapter that delegates to the sidecar.
    /// </summary>
    internal class SidecarTokenCredential(SidecarAccessTokenProvider provider) : TokenCredential
    {
        // Used when the returned token is opaque or has no readable expiry. The sidecar caches tokens,
        // so a short fallback simply causes an earlier (cheap) re-fetch rather than stale-token reuse.
        private static readonly TimeSpan FallbackLifetime = TimeSpan.FromMinutes(5);

        private static readonly JwtSecurityTokenHandler JwtHandler = new();

        private readonly SidecarAccessTokenProvider _provider = provider;

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            // Synchronous path required by TokenCredential contract.
            // Use Task.Run to avoid blocking the current context on ValueTask.
            return Task.Run(() => GetTokenAsync(requestContext, cancellationToken).AsTask(), cancellationToken)
                .GetAwaiter().GetResult();
        }

        public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            var scopes = new List<string>(requestContext.Scopes);
            var token = await _provider.GetAccessTokenAsync(null, scopes, false).ConfigureAwait(false);
            return new AccessToken(token, ResolveExpiry(token));
        }

        private static DateTimeOffset ResolveExpiry(string token)
        {
            // Prefer the token's own "exp" claim so Azure.Core caches/refreshes on the real lifetime.
            try
            {
                if (JwtHandler.CanReadToken(token))
                {
                    var expiry = JwtHandler.ReadJwtToken(token).ValidTo;
                    if (expiry > DateTime.MinValue)
                    {
                        return new DateTimeOffset(DateTime.SpecifyKind(expiry, DateTimeKind.Utc));
                    }
                }
            }
            catch (ArgumentException)
            {
                // Opaque/non-JWT token - fall back below.
            }

            return DateTimeOffset.UtcNow.Add(FallbackLifetime);
        }
    }
}
