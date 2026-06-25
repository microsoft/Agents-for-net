// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IdentityModel.Tokens.Jwt;

namespace Microsoft.Agents.Authentication.EntraAuthSidecar.Utils
{
    /// <summary>
    /// Helpers for interpreting the lifetime of sidecar-issued tokens.
    /// </summary>
    internal static class SidecarTokenExpiry
    {
        /// <summary>
        /// Conservative lifetime applied when the token is opaque or carries no readable JWT <c>exp</c>.
        /// </summary>
        internal static readonly TimeSpan FallbackLifetime = TimeSpan.FromMinutes(5);

        private static readonly JwtSecurityTokenHandler JwtHandler = new();

        /// <summary>
        /// Resolves the absolute expiry of a sidecar-issued token. Prefers the token's own JWT
        /// <c>exp</c> claim; falls back to <see cref="FallbackLifetime"/> from now when the token is
        /// opaque or carries no readable expiry.
        /// </summary>
        /// <param name="token">The raw access token returned by the sidecar.</param>
        /// <returns>The absolute UTC expiry of the token.</returns>
        public static DateTimeOffset Resolve(string token)
        {
            if (!string.IsNullOrEmpty(token))
            {
                try
                {
                    var expiry = JwtHandler.ReadJwtToken(token).ValidTo;
                    if (expiry > DateTime.MinValue)
                    {
                        return new DateTimeOffset(DateTime.SpecifyKind(expiry, DateTimeKind.Utc));
                    }
                }
                catch (ArgumentException)
                {
                    // Opaque/non-JWT token - fall back below.
                }
            }

            return DateTimeOffset.UtcNow.Add(FallbackLifetime);
        }
    }
}
