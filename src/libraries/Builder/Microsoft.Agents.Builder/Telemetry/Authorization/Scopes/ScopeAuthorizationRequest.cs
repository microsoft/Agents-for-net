using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace Microsoft.Agents.Builder.Telemetry.Authorization.Scopes
{
    internal class ScopeAuthorizationRequest
    {
        private readonly string _authHandlerId;
        private readonly string _connectionName;
        private readonly IEnumerable<string>? _scopes;

        public ScopeAuthorizationRequest(string scopeName, string authHandlerId, string connectionName, IEnumerable<string>? scopes) : base(scopeName)
        {
            _authHandlerId = authHandlerId;
            _connectionName = connectionName;
            _scopes = scopes;
        }
    }
}
