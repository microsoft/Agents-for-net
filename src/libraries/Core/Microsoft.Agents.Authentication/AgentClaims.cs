// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;


namespace Microsoft.Agents.Authentication
{
    /// <summary>
    /// Provides utility methods for working with claims in the context of Agent authentication.
    /// </summary>
    public static class AgentClaims
    {
        public static bool IsExchangeableToken(JwtSecurityToken jwtToken)
        {
            AssertionHelpers.ThrowIfNull(jwtToken, nameof(jwtToken));

            var idtyp = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "idtyp")?.Value;
            if ("user".Equals(idtyp))
            {
                return false;
            }

            var aud = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "aud")?.Value;
            var appId = GetAppId(jwtToken);  // this will use either "appid" or "azp" based on 'ver'
            return (bool)(aud?.Contains(appId));
        }

        public static string GetAppId(JwtSecurityToken jwtToken)
        {
            AssertionHelpers.ThrowIfNull(jwtToken, nameof(jwtToken));

            string appIdClaim;
            var ver = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "ver")?.Value ?? "1.0";
            if (ver.Equals("1.0"))
            {
                appIdClaim = jwtToken.Claims?.SingleOrDefault(claim => claim.Type == AuthenticationConstants.AppIdClaim)?.Value;
            }
            else
            {
                appIdClaim = jwtToken.Claims?.SingleOrDefault(claim => claim.Type == AuthenticationConstants.AzpClaim)?.Value;
            }

            return appIdClaim;
        }

        /// <summary>
        /// Retrieves the AppId from the given claims identity.
        /// </summary>
        /// <param name="claimsIdentity">The claims identity containing the token information.</param>
        /// <returns>The AppId as a string, or null if not found.</returns>
        /// <remarks>
        /// For requests from the channel, the AppId is in the Audience claim of the JWT token.
        /// For the emulator, it is in the AppId claim. For unauthenticated requests, anonymous claimsIdentity is provided if auth is disabled.
        /// </remarks>
        public static string GetAppId(ClaimsIdentity claimsIdentity)
        {
            // Verify we have a sensible Claims Identity
            AssertionHelpers.ThrowIfNull(claimsIdentity, nameof(claimsIdentity));
            
            // For requests from channel App Id is in Audience claim of JWT token. For emulator it is in AppId claim. For
            // unauthenticated requests we have anonymous claimsIdentity provided auth is disabled.
            // For Activities coming from Emulator AppId claim contains the Agent's AAD AppId.
            var appIdClaim = claimsIdentity.Claims?.SingleOrDefault(claim => claim.Type == AuthenticationConstants.AudienceClaim);
            appIdClaim ??= claimsIdentity.Claims?.SingleOrDefault(claim => claim.Type == AuthenticationConstants.AppIdClaim);

            return appIdClaim?.Value;
        }

        /// <summary>
        /// Gets the outgoing AppId from a claims list.
        /// </summary>
        /// <remarks>
        /// In v1 tokens the AppId is in the the <see cref="AuthenticationConstants.AppIdClaim"/> claim.
        /// In v2 tokens the AppId is in the azp <see cref="AuthenticationConstants.AuthorizedParty"/> claim.
        /// If the <see cref="AuthenticationConstants.VersionClaim"/> is not present, this method will attempt to
        /// obtain the attribute from the <see cref="AuthenticationConstants.AppIdClaim"/> or if present.
        /// </remarks>
        /// <param name="identity">The Agent identity</param>
        /// <returns>The value of the appId claim if found (null if it can't find a suitable claim).</returns>
        public static string GetOutgoingAppId(ClaimsIdentity identity)
        {
            // Verify we have a sensible Claims Identity
            AssertionHelpers.ThrowIfNull(identity, nameof(identity));

            var claimsList = identity.Claims;
            string appId = null;

            // Depending on Version, the is either in the
            // appid claim (Version 1) or the Authorized Party claim (Version 2).
            var tokenVersion = claimsList.FirstOrDefault(claim => claim.Type == AuthenticationConstants.VersionClaim)?.Value;
            if (string.IsNullOrWhiteSpace(tokenVersion) || tokenVersion == "1.0")
            {
                // either no Version or a version of "1.0" means we should look for
                // the claim in the "appid" claim.
                var appIdClaim = claimsList.FirstOrDefault(c => c.Type == AuthenticationConstants.AppIdClaim);
                appId = appIdClaim?.Value;
            }
            else if (tokenVersion == "2.0")
            {
                // "2.0" puts the AppId in the "azp" claim.
                var appZClaim = claimsList.FirstOrDefault(c => c.Type == AuthenticationConstants.AuthorizedParty);
                appId = appZClaim?.Value;
            }

            return appId;
        }

        /// <summary>
        /// Checks if the given list of claims represents a Agent claim (not coming from ABS/SMBA).
        /// </summary>
        /// <remarks>
        /// A Agent claim should contain:
        ///     An <see cref="AuthenticationConstants.VersionClaim"/> claim.
        ///     An <see cref="AuthenticationConstants.AudienceClaim"/> claim.
        ///     An <see cref="AuthenticationConstants.AppIdClaim"/> claim (v1) or an a <see cref="AuthenticationConstants.AuthorizedParty"/> claim (v2).
        /// And the appId claim should be different than the audience claim.
        /// When a channel (webchat, teams, etc.) invokes an Agent, the <see cref="AuthenticationConstants.AudienceClaim"/>
        /// is set to <see cref="AuthenticationConstants.BotFrameworkTokenIssuer"/> but when an Agent calls another Agent,
        /// the audience claim is set to the appId of the Agent being invoked.
        /// The protocol supports v1 and v2 tokens:
        /// For v1 tokens, the  <see cref="AuthenticationConstants.AppIdClaim"/> is present and set to the app Id of the calling Agent.
        /// For v2 tokens, the  <see cref="AuthenticationConstants.AuthorizedParty"/> is present and set to the app Id of the calling Agent.
        /// </remarks>
        /// <param name="claims">A list of claims.</param>
        /// <returns>True if the list of claims is an Agent claim, false if is not.</returns>
        public static bool IsAgentClaim(ClaimsIdentity claims)
        {
            AssertionHelpers.ThrowIfNull(claims, nameof(claims));

            var claimsList = claims.Claims;

            var version = claimsList.FirstOrDefault(claim => claim.Type == AuthenticationConstants.VersionClaim);
            if (string.IsNullOrWhiteSpace(version?.Value))
            {
                // Must have a version claim.
                return false;
            }

            var audience = claimsList.FirstOrDefault(claim => claim.Type == AuthenticationConstants.AudienceClaim)?.Value;
            if (string.IsNullOrWhiteSpace(audience) || AuthenticationConstants.BotFrameworkTokenIssuer.Equals(audience, StringComparison.OrdinalIgnoreCase))
            {
                // The audience is https://api.botframework.com and not an appId.
                return false;
            }

            var appId = GetOutgoingAppId(claims);
            if (string.IsNullOrWhiteSpace(appId))
            {
                return false;
            }

            // Agent claims must contain and app ID and the AppID must be different than the audience.
            return appId != audience;
        }

        /// <summary>
        /// Retrieves the audience of the token from the given claims identity.
        /// </summary>
        /// <param name="identity">The claims identity containing the token information.</param>
        /// <returns>The token audience as a string.</returns>
        public static string GetTokenAudience(ClaimsIdentity identity)
        {
            return AgentClaims.IsAgentClaim(identity)
                ? $"api://{AgentClaims.GetOutgoingAppId(identity)}"
                : AuthenticationConstants.BotFrameworkScope;
        }

        /// <summary>
        /// Retrieves the token scopes from the given claims identity.
        /// </summary>
        /// <param name="identity">The claims identity containing the token information.</param>
        /// <returns>A list of token scopes, or null if no scopes are found.</returns>
        public static IList<string> GetTokenScopes(ClaimsIdentity identity)
        {
            return AgentClaims.IsAgentClaim(identity)
                ? [$"{AgentClaims.GetOutgoingAppId(identity)}/.default"]
                : null;
        }

        /// <summary>
        /// Determines whether anonymous access is allowed based on the given claims identity.
        /// </summary>
        /// <param name="identity">The claims identity to evaluate.</param>
        /// <returns>True if anonymous access is allowed, otherwise false.</returns>
        public static bool AllowAnonymous(ClaimsIdentity identity)
        {
            return identity != null && !identity.IsAuthenticated && !identity.Claims.Any();
        }

        /// <summary>
        /// Creates an Agent Identity.
        /// </summary>
        /// <param name="audience">The aud of the claim.  Typically the ClientId of the Agent.</param>
        /// <param name="anonymous"></param>
        /// <param name="appId">The appId of the incoming token.</param>
        /// <returns></returns>
        public static ClaimsIdentity CreateIdentity(string audience, bool anonymous = false, string appId = null)
        {
            return anonymous
                ? new ClaimsIdentity()
                : new ClaimsIdentity(
                [
                    new(AuthenticationConstants.AudienceClaim, audience),
                    new(AuthenticationConstants.AppIdClaim, appId ?? audience),
                    new(AuthenticationConstants.VersionClaim, "1.0")
                ]);
        }
    }
}
