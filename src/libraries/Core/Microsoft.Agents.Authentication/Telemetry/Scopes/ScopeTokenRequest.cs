using Microsoft.Agents.Core.Telemetry;
using System;

#nullable enable

namespace Microsoft.Agents.Authentication.Telemetry.Scopes
{
    internal class ScopeTokenRequest : TelemetryScope
    {
        private readonly string _authMethod;

        public ScopeTokenRequest(string scopeName, string authMethod) : base(scopeName)
        {
            _authMethod = authMethod;
        }

        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception? exception)
        {
            telemetryActivity.AddTag(TagNames.AuthMethod, _authMethod);
        }
    }
}
