using System.Collections.Generic;

namespace Microsoft.Agents.Builder.Telemetry.Authorization.Scopes
{
    /// <summary>
    /// A <see cref="ScopeAuthorizationRequest"/> that traces an Azure Bot Framework
    /// OAuth sign-out operation.
    /// </summary>
    internal class ScopeAzureBotSignOut : ScopeAuthorizationRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeAzureBotSignOut"/> class.
        /// </summary>
        /// <param name="authHandlerId">The identifier of the authentication handler processing the request.</param>
        public ScopeAzureBotSignOut(string authHandlerId) : base(Constants.ScopeAzureBotSignOut, authHandlerId)
        {
        }
    }
}