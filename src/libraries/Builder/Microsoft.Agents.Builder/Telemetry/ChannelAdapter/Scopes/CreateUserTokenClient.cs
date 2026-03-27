using Microsoft.Agents.Core.Telemetry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.Telemetry.ChannelAdapter.Scopes
{
    internal class CreateUserTokenClient : TelemetryScope
    {
        private string _tokenServiceEndpoint;
        private IEnumerable<string>? _scopes;

        public CreateUserTokenClient(string tokenServiceEndpoint, IEnumerable<string>? scopes) : base(Constants.ScopeContinueConversation)
        {
            _tokenServiceEndpoint = tokenServiceEndpoint;
            _scopes = scopes;
        }

        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception error)
        {
            telemetryActivity.SetTag(TagNames.TokenServiceEndpoint, _tokenServiceEndpoint);
            telemetryActivity.SetTag(TagNames.AuthScopes, TelemetryUtils.FormatScopes(_scopes));
        }
    }
}
