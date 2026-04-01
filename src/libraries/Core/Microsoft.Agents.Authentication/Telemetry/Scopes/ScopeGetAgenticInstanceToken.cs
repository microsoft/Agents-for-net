using Microsoft.Agents.Core.Telemetry;
using System;

#nullable enable

namespace Microsoft.Agents.Authentication.Telemetry.Scopes
{
    internal class ScopeGetAgenticInstanceToken : ScopeTokenRequest
    {
        private readonly string _agenticInstanceId;

        public ScopeGetAgenticInstanceToken(string agenticInstanceId)
            : base(Constants.ScopeGetAgenticInstanceToken, Constants.AuthMethodAgenticInstance)
        {
            _agenticInstanceId = agenticInstanceId;
        }

        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception? error)
        {
            base.Callback(telemetryActivity, duration, error);
            telemetryActivity.SetTag(TagNames.AgenticInstanceId, _agenticInstanceId);
        }
    }
}
