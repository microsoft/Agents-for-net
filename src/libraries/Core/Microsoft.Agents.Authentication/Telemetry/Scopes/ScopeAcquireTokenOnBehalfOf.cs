using Microsoft.Agents.Core.Telemetry;
using System;
using System.Collections.Generic;

#nullable enable

namespace Microsoft.Agents.Authentication.Telemetry.Scopes
{
    internal class ScopeAcquireTokenOnBehalfOf : ScopeTokenRequest
    {
        private readonly IEnumerable<string> _scopes;
        public ScopeAcquireTokenOnBehalfOf(IEnumerable<string> scopes) : base(Constants.ScopeAcquireTokenOnBehalfOf, Constants.AuthMethodOBO)
        {
            _scopes = scopes;
        }

        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception? error)
        {
            base.Callback(telemetryActivity, duration, error);
            telemetryActivity.SetTag(TagNames.AuthScopes, TelemetryUtils.FormatScopes(_scopes));
        }
    }
}
