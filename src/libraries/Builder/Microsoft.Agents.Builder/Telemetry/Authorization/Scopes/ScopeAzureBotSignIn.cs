using System.Collections.Generic;

namespace Microsoft.Agents.Builder.Telemetry.Authorization.Scopes
{
    internal class ScopeAzureBotSignIn : ScopeAuthorizationRequest
    {
        public ScopeAzureBotSignIn(string authHandlerId, string? exchangeConnection, IEnumerable<string>? scopes) : base(Constants.ScopeAzureBotSignIn, authHandlerId, exchangeConnection, scopes)
        {
        }
    }
}
