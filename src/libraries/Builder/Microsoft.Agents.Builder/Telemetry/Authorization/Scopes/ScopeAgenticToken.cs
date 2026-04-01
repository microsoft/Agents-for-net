using System.Collections.Generic;

namespace Microsoft.Agents.Builder.Telemetry.Authorization.Scopes
{
    internal class ScopeAgenticToken : ScopeAuthorizationRequest
    {
        public ScopeAgenticToken(string authHandlerId, string connectionName, IEnumerable<string>? scopes) : base(Constants.ScopeAgenticToken, authHandlerId, connectionName, scopes)
        {
        }
    }
}
