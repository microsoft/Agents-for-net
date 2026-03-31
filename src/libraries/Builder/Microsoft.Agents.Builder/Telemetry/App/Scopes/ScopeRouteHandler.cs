using Microsoft.Agents.Core.Telemetry;
using System;
using System.Diagnostics;

namespace Microsoft.Agents.Builder.Telemetry.App.Scopes
{
    internal class ScopeRouteHandler : TelemetryScope
    {
        private readonly bool _isInvoke;
        private readonly bool _isAgentic;

        public ScopeRouteHandler(bool isInvoke, bool isAgentic) : base(Constants.ScopeRouteHandler)
        {
            _isInvoke = isInvoke;
            _isAgentic = isAgentic;
        }

        protected override void Callback(Activity activity, double duration, Exception? exception)
        {
            activity.SetTag(TagNames.RouteIsInvoke, _isInvoke);
            activity.SetTag(TagNames.RouteIsAgentic, _isAgentic);
        }
    }
}
