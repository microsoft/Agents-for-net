// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Authentication
{
    /// <summary>
    /// Values and constants used for authentication and authorization by the activity protocol.
    /// </summary>
    public static class AuthenticationConstants
    {
        /// <summary>
        /// Bot Framework OAuth scope to request.
        /// </summary>
        public const string BotFrameworkScope = "https://api.botframework.com";

        /// <summary>
        /// Token issuer for ABS tokens.
        /// </summary>
        public const string BotFrameworkTokenIssuer = "https://api.botframework.com";

        /// <summary>
        /// Default OAuth Url used to get a token from IUserTokenClient.
        /// </summary>
        public const string BotFrameworkOAuthUrl = "https://api.botframework.com";

        /// <summary>
        /// The OpenID metadata URL for the public Azure Bot Service.
        /// </summary>
        public const string PublicAzureBotServiceOpenIdMetadataUrl = "https://login.botframework.com/v1/.well-known/openidconfiguration";

        /// <summary>
        /// The OpenID metadata URL for public Azure Active Directory.
        /// </summary>
        public const string PublicOpenIdMetadataUrl = "https://login.microsoftonline.com/common/v2.0/.well-known/openid-configuration";

        /// <summary>
        /// The OpenID metadata URL format for enterprise channels.
        /// </summary>
        public const string EnterpriseChannelOpenIdMetadataUrlFormat = "https://{0}.enterprisechannel.botframework.com/v1/.well-known/openidconfiguration";

        /// <summary>
        /// The OpenID metadata URL for the government Azure Bot Service.
        /// </summary>
        public const string GovAzureBotServiceOpenIdMetadataUrl = "https://login.botframework.azure.us/v1/.well-known/openidconfiguration";

        /// <summary>
        /// The OpenID metadata URL for government Azure Active Directory.
        /// </summary>
        public const string GovOpenIdMetadataUrl = "https://login.microsoftonline.us/cab8a31a-1906-4287-a0d8-4eef66b95f6e/v2.0/.well-known/openid-configuration";

        /// <summary>
        /// The V1 Azure AD token issuer URL template that will contain the tenant id where the token was issued from.
        /// </summary>
        public const string ValidTokenIssuerUrlTemplateV1 = "https://sts.windows.net/{0}/";

        /// <summary>
        /// The V2 Azure AD token issuer URL template that will contain the tenant id where the token was issued from.
        /// </summary>
        public const string ValidTokenIssuerUrlTemplateV2 = "https://login.microsoftonline.com/{0}/v2.0";

        /// <summary>
        /// "azp" Claim.
        /// </summary>
        /// <remarks>
        /// Authorized party - the party to which the ID Token was issued.
        /// This claim follows the general format set forth in the OpenID Spec. http://openid.net/specs/openid-connect-core-1_0.html#IDToken.
        /// </remarks>
        public const string AuthorizedParty = "azp";

        /// <summary>
        /// Audience Claim. 
        /// </summary>
        /// <remarks>
        /// The "aud" (audience) claim identifies the recipients that the JWT is
        /// intended for. Each principal intended to process the JWT MUST
        /// identify itself with a value in the audience claim. If the principal
        /// processing the claim does not identify itself with a value in the
        /// "aud" claim when this claim is present, then the JWT MUST be
        /// rejected. In the general case, the "aud" value is an array of case-
        /// sensitive strings, each containing a StringOrURI value. In the
        /// special case when the JWT has one audience, the "aud" value MAY be a
        /// single case-sensitive string containing a StringOrURI value. The
        /// interpretation of audience values is generally application specific.
        /// Use of this claim is OPTIONAL.
        /// 
        /// For more details, see <a href="https://tools.ietf.org/html/rfc7519#section-4.1.3">RFC 7519 Section 4.1.3</a>.
        /// </remarks>
        public const string AudienceClaim = "aud";

        /// <summary>
        /// Token iss claim name. As used in Microsoft AAD tokens.
        /// </summary>
        public const string IssuerClaim = "iss";

        /// <summary>
        /// Token version claim name. As used in Microsoft AAD tokens.
        /// </summary>
        public const string VersionClaim = "ver";

        /// <summary>
        /// App ID claim name. As used in Microsoft AAD 1.0 tokens.
        /// </summary>
        public const string AppIdClaim = "appid";

        /// <summary>
        /// Tenant Id claim name. As used in Microsoft AAD tokens.
        /// </summary>
        public const string TenantIdClaim = "tid";
    }
}
