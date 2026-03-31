using Microsoft.Agents.Core.Telemetry;

namespace Microsoft.Agents.Builder.Telemetry.App.Scopes
{
    internal class ScopeBeforeTurn : TelemetryScope
    {
        public ScopeBeforeTurn() : base(Constants.ScopeBeforeTurn)
        {
        }
    }
}
