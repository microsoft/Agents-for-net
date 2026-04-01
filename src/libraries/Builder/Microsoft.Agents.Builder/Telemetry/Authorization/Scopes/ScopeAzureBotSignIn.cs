using System.Collections.Generic;

namespace Microsoft.Agents.Builder.Telemetry.Authorization.Scopes
{
    /// <summary>
    /// A <see cref="ScopeAuthorizationRequest"/> that traces an Azure Bot Framework
    /// OAuth sign-in operation.
    /// </summary>
    internal class ScopeAzureBotSignIn : ScopeAuthorizationRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeAzureBotSignIn"/> class.
        /// </summary>
        /// <param name="authHandlerId">The identifier of the authentication handler processing the request.</param>
        /// <param name="exchangeConnection">The OAuth connection name, or <c>null</c>.</param>
        /// <param name="scopes">The OAuth/OIDC scopes requested, or <c>null</c>.</param>
        public ScopeAzureBotSignIn(string authHandlerId, string? exchangeConnection, IEnumerable<string>? scopes) : base(Constants.ScopeAzureBotSignIn, authHandlerId, exchangeConnection, scopes)
        {
        }
    }
}
