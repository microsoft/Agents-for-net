// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace Microsoft.Agents.Authentication
{
    public static class BotClaims
    {
        public static string GetAppId(ClaimsIdentity claimsIdentity)
        {
            // Verify we have a sensible Claims Identity
            ArgumentNullException.ThrowIfNull(claimsIdentity);
            
            // For requests from channel App Id is in Audience claim of JWT token. For emulator it is in AppId claim. For
            // unauthenticated requests we have anonymous claimsIdentity provided auth is disabled.
            // For Activities coming from Emulator AppId claim contains the Bot's AAD AppId.
            var botAppIdClaim = claimsIdentity.Claims?.SingleOrDefault(claim => claim.Type == AuthenticationConstants.AudienceClaim);
            if (botAppIdClaim == null)
            {
                botAppIdClaim = claimsIdentity.Claims?.SingleOrDefault(claim => claim.Type == AuthenticationConstants.AppIdClaim);
            }

            return botAppIdClaim?.Value;
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
        /// <param name="claims">A list of <see cref="Claim"/> instances.</param>
        /// <returns>The value of the appId claim if found (null if it can't find a suitable claim).</returns>
        public static string GetOutgoingAppId(IEnumerable<Claim> claims)
        {
            // Verify we have a sensible Claims Identity
            ArgumentNullException.ThrowIfNull(claims);

            var claimsList = claims as IList<Claim> ?? claims.ToList();
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
        /// Checks if the given list of claims represents a bot claim (not coming from ABS/SMBA).
        /// </summary>
        /// <remarks>
        /// A bot claim should contain:
        ///     An <see cref="AuthenticationConstants.VersionClaim"/> claim.
        ///     An <see cref="AuthenticationConstants.AudienceClaim"/> claim.
        ///     An <see cref="AuthenticationConstants.AppIdClaim"/> claim (v1) or an a <see cref="AuthenticationConstants.AuthorizedParty"/> claim (v2).
        /// And the appId claim should be different than the audience claim.
        /// When a channel (webchat, teams, etc.) invokes a bot, the <see cref="AuthenticationConstants.AudienceClaim"/>
        /// is set to <see cref="AuthenticationConstants.BotFrameworkTokenIssuer"/> but when a bot calls another bot,
        /// the audience claim is set to the appId of the bot being invoked.
        /// The protocol supports v1 and v2 tokens:
        /// For v1 tokens, the  <see cref="AuthenticationConstants.AppIdClaim"/> is present and set to the app Id of the calling bot.
        /// For v2 tokens, the  <see cref="AuthenticationConstants.AuthorizedParty"/> is present and set to the app Id of the calling bot.
        /// </remarks>
        /// <param name="claims">A list of claims.</param>
        /// <returns>True if the list of claims is a bot claim, false if is not.</returns>
        public static bool IsBotClaim(IEnumerable<Claim> claims)
        {
            var claimsList = claims.ToList();

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

            var appId = GetOutgoingAppId(claimsList);
            if (string.IsNullOrWhiteSpace(appId))
            {
                return false;
            }

            // Bot claims must contain and app ID and the AppID must be different than the audience.
            return appId != audience;
        }

        public static string GetTokenAudience(IEnumerable<Claim> claims)
        {
            // Temporarily setting audience to api uri so that MSAL works.  This needs to be resolved.
            return $"app://{BotClaims.GetOutgoingAppId(claims)}";
        }
    }
}
