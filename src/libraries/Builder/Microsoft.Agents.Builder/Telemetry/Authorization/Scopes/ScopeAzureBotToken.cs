using System.Collections.Generic;

namespace Microsoft.Agents.Builder.Telemetry.Authorization.Scopes
{
    internal class ScopeAzureBotToken : ScopeAuthorizationRequest
    {
        public ScopeAzureBotToken(string authHandlerId, string connectionName, IEnumerable<string>? scopes) : base(Constants.ScopeAzureBotToken, authHandlerId, connectionName, scopes)
        {
        }
    }
}
