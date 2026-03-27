using Microsoft.Agents.Core.Telemetry;
using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Builder.Telemetry.ChannelAdapter.Scopes
{
    internal class ScopeCreateUserTokenClient : TelemetryScope
    {
        private string _tokenServiceEndpoint;
        private IEnumerable<string>? _scopes;

        public ScopeCreateUserTokenClient(string tokenServiceEndpoint) : base(Constants.ScopeContinueConversation)
        {
            _tokenServiceEndpoint = tokenServiceEndpoint;
        }

        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception error)
        {
            telemetryActivity.SetTag(TagNames.TokenServiceEndpoint, _tokenServiceEndpoint);
        }
    }
}
