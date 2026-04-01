using System.Collections.Generic;

namespace Microsoft.Agents.Builder.Telemetry.Authorization.Scopes
{
    internal class ScopeAzureBotToken : ScopeAuthorizationRequest
    {
        public ScopeAzureBotToken(string authHandlerId, string? exchangeConnection, IEnumerable<string>? scopes) : base(Constants.ScopeAzureBotToken, authHandlerId, exchangeConnection, scopes)
        {
        }
    }
}
