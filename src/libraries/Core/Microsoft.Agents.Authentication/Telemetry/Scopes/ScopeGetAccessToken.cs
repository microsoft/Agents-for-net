using Microsoft.Agents.Core.Telemetry;
using System;
using System.Collections.Generic;

#nullable enable

namespace Microsoft.Agents.Authentication.Telemetry.Scopes
{
    internal class ScopeGetAccessToken : ScopeTokenRequest
    {
        private readonly IEnumerable<string> _scopes;

        public ScopeGetAccessToken(IEnumerable<string> scopes, string authMethod) : base(Constants.ScopeGetAccessToken, authMethod)
        {
            _scopes = scopes;
        }

        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception? exception)
        {
            base.Callback(telemetryActivity, duration, exception);
            telemetryActivity.SetTag(TagNames.AuthScopes, TelemetryUtils.FormatScopes(_scopes));
        }
    }
}
