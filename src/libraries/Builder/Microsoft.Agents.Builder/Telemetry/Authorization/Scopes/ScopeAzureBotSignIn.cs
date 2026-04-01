using System.Collections.Generic;

namespace Microsoft.Agents.Builder.Telemetry.Authorization.Scopes
{
    internal class ScopeAzureBotSignIn : ScopeAuthorizationRequest
    {
        public ScopeAzureBotSignIn(string authHandlerId, string connectionName, IEnumerable<string>? scopes) : base(Constants.ScopeAzureBotSignIn, authHandlerId, connectionName, scopes)
        {
        }
    }
}
