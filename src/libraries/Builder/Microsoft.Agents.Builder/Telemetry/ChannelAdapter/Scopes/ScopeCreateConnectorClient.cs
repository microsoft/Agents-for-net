using Microsoft.Agents.Core.Telemetry;
using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Builder.Telemetry.ChannelAdapter.Scopes
{
    internal class ScopeCreateConnectorClient : TelemetryScope
    {
        private string _serviceUrl;
        private IEnumerable<string>? _scopes;
        private bool _isAgenticRequest;

        public ScopeCreateConnectorClient(string serviceUrl, IEnumerable<string>? scopes, bool isAgenticRequest) : base(Constants.ScopeContinueConversation)
        {
            _serviceUrl = serviceUrl;
            _scopes = scopes;
            _isAgenticRequest = isAgenticRequest;
        }

        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception error)
        {
            telemetryActivity.SetTag(TagNames.ServiceUrl, _serviceUrl);
            telemetryActivity.SetTag(TagNames.AuthScopes, TelemetryUtils.FormatScopes(_scopes));
            telemetryActivity.SetTag(TagNames.IsAgentic, _isAgenticRequest);
        }
    }
}
