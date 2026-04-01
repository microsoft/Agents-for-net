using System.Collections.Generic;

namespace Microsoft.Agents.Builder.Telemetry.Authorization.Scopes
{
    internal class ScopeAzureBotSignOut : ScopeAuthorizationRequest
    {
        public ScopeAzureBotSignOut(string authHandlerId) : base(Constants.ScopeAzureBotSignOut, authHandlerId)
        {
        }
    }
}