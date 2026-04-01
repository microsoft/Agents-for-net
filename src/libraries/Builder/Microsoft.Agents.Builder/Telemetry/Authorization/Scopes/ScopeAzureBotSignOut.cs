using System.Collections.Generic;

namespace Microsoft.Agents.Builder.Telemetry.Authorization.Scopes
{
    internal class ScopeAzureBotSignOut : ScopeAuthorizationRequest
    {
        public ScopeAzureBotSignOut(string authHandlerId, string connectionName, IEnumerable<string>? scopes) : base(Constants.ScopeAzureBotSignOut, authHandlerId, connectionName, scopes)
        {
        }
    }
}