using Microsoft.Agents.Core.Telemetry;
using System;
using System.Collections.Generic;

#nullable enable

namespace Microsoft.Agents.Authentication.Telemetry.Scopes
{
    internal class ScopeGetAgenticUserToken : ScopeTokenRequest
    {
        private readonly string _agenticInstanceId;
        private readonly string _agenticUserId;
        private readonly IEnumerable<string>? _scopes;

        public ScopeGetAgenticUserToken(string agenticInstanceId, string agenticUserId, IEnumerable<string>? scopes)
            : base(Constants.ScopeGetAgenticInstanceToken, Constants.AuthMethodAgenticInstance)
        {
            _agenticInstanceId = agenticInstanceId;
            _agenticUserId = agenticUserId;
            _scopes = scopes;
        }

        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception? error)
        {
            base.Callback(telemetryActivity, duration, error);
            telemetryActivity.SetTag(TagNames.AgenticInstanceId, _agenticInstanceId);
            telemetryActivity.SetTag(TagNames.AgenticUserId, _agenticUserId);
            telemetryActivity.SetTag(TagNames.AuthScopes, TelemetryUtils.FormatScopes(_scopes));
        }
    }
}
