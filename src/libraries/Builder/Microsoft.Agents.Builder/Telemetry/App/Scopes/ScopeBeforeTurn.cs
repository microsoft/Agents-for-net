using Microsoft.Agents.Core.Telemetry;

namespace Microsoft.Agents.Builder.Telemetry.App.Scopes
{
    /// <summary>
    /// A <see cref="TelemetryScope"/> that traces the before-turn middleware pipeline
    /// execution for a turn.
    /// </summary>
    internal class ScopeBeforeTurn : TelemetryScope
    {
        public ScopeBeforeTurn() : base(Constants.ScopeBeforeTurn)
        {
        }
    }
}
